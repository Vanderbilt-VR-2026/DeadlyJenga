using System;
using System.Collections.Generic;

namespace DeadlyJenga.CastlePrototype
{
    // Pure authority model. Adapter must persist each mutation before executing its outbox.
    public enum GameFlowPhase { Idle, Choosing, Selected, WaitingReady, LoadingGame, Playing, Results, LoadingLobby, Failed }
    public enum GameFlowEffect { None, ChooseProfile, LoadGame, ReturnLobby }

    [Serializable]
    public sealed class GameFlowTimings
    {
        // Trial values; the requested initial random-lobby choice delay remains 30 seconds.
        public double selection = 8, results = 12, sceneTimeout = 25, profileTimeout = 5;
    }

    [Serializable]
    public sealed class GameFlowState
    {
        public string roomId = "", profileId = "", operationId = "", error = "";
        public int authorityTerm, revision, selectionVersion, rosterVersion, round, operationSequence, attempts;
        public string[] humanIds = Array.Empty<string>(), readyIds = Array.Empty<string>(), launchIds = Array.Empty<string>();
        public string[] playlistIds = Array.Empty<string>();
        public string[] withdrawnIds = Array.Empty<string>();
        public string modeStartOperation="";
        public int modeStartRound;
        public bool demo, automatic, startRequested;
        public GameFlowPhase phase;
        public GameFlowEffect pending;
        public double remaining;
        public GameFlowTimings timings = new GameFlowTimings();
    }

    public sealed class RoomGameFlow
    {
        const double ChooseDelay = 30;
        readonly GameFlowState data;
        public int Revision=>data.revision;
        public GameFlowState State => Copy(data);
        public RoomGameFlow(GameFlowState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Validate(state); data = Copy(state);
        }

        static GameFlowState Copy(GameFlowState s) => new GameFlowState
        {
            roomId=s.roomId, profileId=s.profileId, operationId=s.operationId, error=s.error,
            authorityTerm=s.authorityTerm, revision=s.revision, selectionVersion=s.selectionVersion,
            rosterVersion=s.rosterVersion, round=s.round, operationSequence=s.operationSequence, attempts=s.attempts,
            demo=s.demo, automatic=s.automatic, startRequested=s.startRequested, phase=s.phase,
            pending=s.pending, remaining=s.remaining,
            humanIds=(string[])s.humanIds.Clone(), readyIds=(string[])s.readyIds.Clone(), launchIds=(string[])s.launchIds.Clone(),
            playlistIds=(string[])s.playlistIds.Clone(),
            withdrawnIds=(string[])s.withdrawnIds.Clone(),
            modeStartOperation=s.modeStartOperation,modeStartRound=s.modeStartRound,
            timings=new GameFlowTimings { selection=s.timings.selection, results=s.timings.results, sceneTimeout=s.timings.sceneTimeout, profileTimeout=s.timings.profileTimeout }
        };

        static bool ValidIds(string[] ids)
        {
            if (ids == null || ids.Length > 10) return false;
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids) if (string.IsNullOrEmpty(id) || id.Length > 128 || !seen.Add(id)) return false;
            return true;
        }

        public static void Validate(GameFlowState s)
        {
            if (s == null || string.IsNullOrEmpty(s.roomId) || s.roomId.Length > 80 ||
                s.profileId == null || s.profileId.Length > 128 || s.operationId == null || s.operationId.Length > 128 ||
                s.error == null || s.error.Length > 240 || s.authorityTerm < 0 || s.revision < 0 || s.selectionVersion < 0 || s.rosterVersion < 0 ||
                s.modeStartOperation==null||s.modeStartOperation.Length>128||s.modeStartRound<0||s.modeStartRound>s.round||
                s.round < 0 || s.operationSequence < 0 || s.attempts < 0 || s.attempts > 3 ||
                !Enum.IsDefined(typeof(GameFlowPhase), s.phase) || !Enum.IsDefined(typeof(GameFlowEffect), s.pending) ||
                double.IsNaN(s.remaining) || double.IsInfinity(s.remaining) || s.remaining < 0 || s.remaining > 300 ||
                s.timings == null || !ValidDelay(s.timings.selection) || !ValidDelay(s.timings.results) || !ValidDelay(s.timings.sceneTimeout) || !ValidDelay(s.timings.profileTimeout))
                throw new ArgumentException("Invalid room flow snapshot.");
            bool loading = s.phase == GameFlowPhase.LoadingGame || s.phase == GameFlowPhase.LoadingLobby;
            bool choosing = s.phase == GameFlowPhase.Choosing && s.pending == GameFlowEffect.ChooseProfile;
            bool failed = s.phase == GameFlowPhase.Failed;
            if (s.pending == GameFlowEffect.None ? loading : string.IsNullOrEmpty(s.operationId) || !(loading || choosing || failed))
                throw new ArgumentException("Invalid room flow operation.");
            if (s.phase == GameFlowPhase.LoadingGame && s.pending != GameFlowEffect.LoadGame ||
                s.phase == GameFlowPhase.LoadingLobby && s.pending != GameFlowEffect.ReturnLobby)
                throw new ArgumentException("Transition destination mismatch.");
            if ((s.phase == GameFlowPhase.Selected || s.phase == GameFlowPhase.WaitingReady || s.phase == GameFlowPhase.LoadingGame ||
                s.phase == GameFlowPhase.Playing || s.phase == GameFlowPhase.Results) && string.IsNullOrEmpty(s.profileId))
                throw new ArgumentException("Game phase needs a selected profile.");
            if (s.demo && (s.automatic || s.phase != GameFlowPhase.Idle || s.pending != GameFlowEffect.None))
                throw new ArgumentException("Demo cannot own an automatic Game transition.");
            if (!ValidIds(s.humanIds) || !ValidIds(s.readyIds) || !ValidIds(s.launchIds) || !ValidIds(s.playlistIds) || !ValidIds(s.withdrawnIds))
                throw new ArgumentException("Invalid participant ledger.");
            foreach (var id in s.readyIds) if (Array.IndexOf(s.humanIds,id) < 0) throw new ArgumentException("Ready actor is absent.");
            foreach (var id in s.playlistIds) if (Array.IndexOf(s.humanIds,id) < 0) throw new ArgumentException("Playlist actor is absent.");
            foreach (var id in s.withdrawnIds) if (Array.IndexOf(s.humanIds,id) < 0) throw new ArgumentException("Withdrawn actor is absent.");
            if (s.pending == GameFlowEffect.None && (s.operationId.Length != 0 || s.attempts != 0))
                throw new ArgumentException("Completed operation retains an active receipt.");
            if (s.pending != GameFlowEffect.None && (s.operationSequence < 1 || s.operationId != s.roomId+"/"+s.operationSequence || s.attempts < 1))
                throw new ArgumentException("Invalid operation generation.");
            if (s.pending == GameFlowEffect.LoadGame && (s.phase != GameFlowPhase.LoadingGame && !failed || string.IsNullOrEmpty(s.profileId) || s.round < 1 || s.launchIds.Length == 0))
                throw new ArgumentException("Game operation lacks its profile, round or committed cohort.");
            if (s.pending == GameFlowEffect.ReturnLobby && s.phase != GameFlowPhase.LoadingLobby && !failed)
                throw new ArgumentException("Lobby operation is in the wrong phase.");
            if (s.pending == GameFlowEffect.ChooseProfile && !choosing && !failed)
                throw new ArgumentException("Profile operation is in the wrong phase.");
            if ((s.phase == GameFlowPhase.Playing || s.phase == GameFlowPhase.Results) && (s.round < 1 || s.launchIds.Length == 0))
                throw new ArgumentException("Active round lacks its committed identity.");
            if (s.phase == GameFlowPhase.Failed && string.IsNullOrEmpty(s.error))
                throw new ArgumentException("Failed state needs a reason.");
        }

        static bool ValidDelay(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 1 && value <= 300;

        bool Owns(int term) => term == data.authorityTerm;
        bool Transitioning => data.pending == GameFlowEffect.LoadGame || data.pending == GameFlowEffect.ReturnLobby;
        void Changed() { checked { data.revision++; } }
        void ClearOperation() { data.pending = GameFlowEffect.None; data.operationId = ""; data.attempts = 0; data.error = ""; }
        void ClearReady() { data.readyIds=Array.Empty<string>(); data.startRequested=false; }
        void Begin(GameFlowEffect effect, GameFlowPhase phase, double timeout)
        {
            checked { data.operationSequence++; }
            data.pending = effect; data.operationId = data.roomId + "/" + data.operationSequence;
            data.attempts = 1; data.phase = phase; data.remaining = timeout; data.error = "";
        }

        public bool SetRoster(int term, string[] ids)
        {
            if (!Owns(term) || !ValidIds(ids)) return false;
            var sorted=(string[])ids.Clone(); Array.Sort(sorted,StringComparer.Ordinal);
            var old=(string[])data.humanIds.Clone(); Array.Sort(old,StringComparer.Ordinal);
            bool same=old.Length==sorted.Length;
            for (int i=0;same && i<old.Length;i++) same=old[i]==sorted[i];
            if (same) return false;
            data.humanIds=sorted; checked { data.rosterVersion++; } ClearReady();
            data.playlistIds=Array.FindAll(data.playlistIds,id=>Array.IndexOf(sorted,id)>=0);
            data.withdrawnIds=Array.FindAll(data.withdrawnIds,id=>Array.IndexOf(sorted,id)>=0);
            if (!Transitioning && (data.phase==GameFlowPhase.Selected || data.phase==GameFlowPhase.WaitingReady))
            { checked { data.selectionVersion++; } data.phase=GameFlowPhase.Selected; data.remaining=data.timings.selection; }
            if (ids.Length==0 && !Transitioning && data.phase!=GameFlowPhase.Playing && data.phase!=GameFlowPhase.Results)
            { ClearOperation(); data.phase=GameFlowPhase.Idle; data.remaining=0; data.profileId=""; data.launchIds=Array.Empty<string>(); }
            Changed(); return true;
        }

        public bool SetReady(int term, string actor, int selectionVersion, int rosterVersion, bool ready)
        {
            if (!Owns(term) || selectionVersion!=data.selectionVersion || rosterVersion!=data.rosterVersion ||
                Array.IndexOf(data.humanIds,actor)<0 || data.phase!=GameFlowPhase.Selected && data.phase!=GameFlowPhase.WaitingReady) return false;
            var present=Array.IndexOf(data.readyIds,actor)>=0;
            if (ready==present && (ready || !data.startRequested)) return false;
            if (ready) { var votes=new List<string>(data.readyIds); votes.Add(actor); data.readyIds=votes.ToArray(); data.withdrawnIds=Array.FindAll(data.withdrawnIds,id=>id!=actor); }
            else
            {
                // Unready invalidates queued Start and old readiness receipts before commitment.
                ClearReady(); checked { data.selectionVersion++; }
                data.phase=GameFlowPhase.Selected; data.remaining=data.timings.selection;
            }
            Changed(); return true;
        }

        public bool Configure(int term, bool demo, bool automatic)
        {
            if (!Owns(term) || Transitioning || !demo && (data.phase == GameFlowPhase.Playing || data.phase == GameFlowPhase.Results)) return false;
            data.demo = demo; data.automatic = !demo && automatic; data.profileId = ""; data.startRequested = false;
            data.playlistIds=Array.Empty<string>();
            data.withdrawnIds=Array.Empty<string>();
            ClearReady(); data.launchIds=Array.Empty<string>();
            checked { data.selectionVersion++; }
            ClearOperation(); data.phase = data.automatic ? GameFlowPhase.Choosing : GameFlowPhase.Idle;
            data.remaining = data.automatic ? ChooseDelay : 0; Changed(); return true;
        }

        // Ready acknowledgements bind to selectionVersion and the current connected roster.
        public bool Select(int term, string profileId, bool eligible)
        {
            if (!Owns(term) || data.demo || !eligible || string.IsNullOrEmpty(profileId) || profileId.Length > 128 ||
                Transitioning || data.phase == GameFlowPhase.Playing || data.phase == GameFlowPhase.Results) return false;
            data.profileId = profileId; data.startRequested = false; checked { data.selectionVersion++; }
            ClearReady();
            ClearOperation(); data.phase = GameFlowPhase.Selected;
            data.remaining = data.timings.selection; Changed(); return true;
        }

        public bool Tick(int term, double seconds, bool eligible, bool lobbyVisible)
        {
            if (!Owns(term) || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > 5) return false;
            if (data.demo) return false;
            int humans=data.humanIds.Length;
            bool everyoneReady=humans>0 && data.readyIds.Length==humans;
            // A lobby visit from a live match is never a new automatic selection.
            if (data.phase == GameFlowPhase.Idle && data.automatic && humans > 0 && lobbyVisible)
            { data.phase = GameFlowPhase.Choosing; data.remaining = ChooseDelay; Changed(); return true; }
            if (humans == 0 && !Transitioning)
            {
                // Stop scheduling without lying about an existing loaded round or its results.
                if (data.phase==GameFlowPhase.Playing || data.phase==GameFlowPhase.Results) return false;
                if (data.phase == GameFlowPhase.Idle && data.pending == GameFlowEffect.None) return false;
                ClearReady(); ClearOperation(); data.phase = GameFlowPhase.Idle; data.remaining = 0; data.profileId = ""; data.launchIds=Array.Empty<string>(); Changed(); return true;
            }
            if (data.phase == GameFlowPhase.Failed || data.phase == GameFlowPhase.Playing || data.phase == GameFlowPhase.Idle) return false;
            if (data.pending == GameFlowEffect.ChooseProfile)
            {
                if (seconds==0) return false;
                data.remaining=Math.Max(0,data.remaining-seconds);
                if (data.remaining==0) { data.phase=GameFlowPhase.Failed; data.error="Choosing a game timed out. Retry or change the room setup."; }
                Changed(); return true;
            }
            // Do not advance lobby countdown while its scene or peer setup is unavailable.
            if (!lobbyVisible && !Transitioning && data.phase != GameFlowPhase.Results) return false;
            double before = data.remaining;
            var beforePhase = data.phase;
            data.remaining = Math.Max(0, data.remaining - seconds);
            if (Transitioning && data.remaining == 0)
            { data.phase = GameFlowPhase.Failed; data.error = "Scene transition timed out. Retry or leave the room."; }
            else if (data.phase == GameFlowPhase.Choosing && data.remaining == 0)
                Begin(GameFlowEffect.ChooseProfile, GameFlowPhase.Choosing, data.timings.profileTimeout);
            else if (data.phase == GameFlowPhase.Selected && data.remaining == 0)
                data.phase = GameFlowPhase.WaitingReady;
            else if (data.phase == GameFlowPhase.WaitingReady && everyoneReady && eligible && humans > 0 && (data.automatic || data.startRequested))
            { checked { data.round++; } data.launchIds=(string[])data.humanIds.Clone(); ClearReady(); Begin(GameFlowEffect.LoadGame, GameFlowPhase.LoadingGame, data.timings.sceneTimeout); }
            else if (data.phase == GameFlowPhase.Results && data.automatic && data.remaining == 0)
                Begin(GameFlowEffect.ReturnLobby, GameFlowPhase.LoadingLobby, data.timings.sceneTimeout);
            if (before == data.remaining && beforePhase == data.phase) return false;
            Changed(); return true;
        }

        public bool ChooseCompleted(int term, string operation, int attempt, string profile, bool eligible)
        {
            if (!Owns(term) || data.pending != GameFlowEffect.ChooseProfile || data.operationId != operation || data.attempts!=attempt) return false;
            if (!eligible || string.IsNullOrEmpty(profile) || profile.Length > 128)
            { data.phase = GameFlowPhase.Failed; data.error = "No compatible curated profile is available."; Changed(); return true; }
            return Select(term, profile, true);
        }

        public bool RequestStart(int term, int selectionVersion, int rosterVersion)
        {
            if (!Owns(term) || data.demo || data.phase != GameFlowPhase.WaitingReady || string.IsNullOrEmpty(data.profileId) ||
                selectionVersion!=data.selectionVersion || rosterVersion!=data.rosterVersion) return false;
            data.startRequested = true; Changed(); return true;
        }

        public bool EnrollPlaylist(int term,string actor,bool value)
        {
            if(!Owns(term)||data.demo||Transitioning||Array.IndexOf(data.humanIds,actor)<0)return false;
            bool enrolled=Array.IndexOf(data.playlistIds,actor)>=0;
            if(enrolled==value)return false;
            var ids=new List<string>(data.playlistIds);
            if(value)ids.Add(actor);else ids.Remove(actor);
            data.playlistIds=ids.ToArray();Changed();return true;
        }

        public bool RequestRematch(int term)
        {
            if(!Owns(term)||data.demo||data.phase!=GameFlowPhase.Results||data.humanIds.Length==0)return false;
            var cohort=Array.FindAll(data.humanIds,id=>Array.IndexOf(data.launchIds,id)>=0&&Array.IndexOf(data.withdrawnIds,id)<0||Array.IndexOf(data.playlistIds,id)>=0);
            if(cohort.Length==0)return false;
            checked{data.round++;}
            data.launchIds=cohort;ClearReady();
            Begin(GameFlowEffect.LoadGame,GameFlowPhase.LoadingGame,data.timings.sceneTimeout);
            Changed();return true;
        }

        public bool WithdrawAutomaticConsent(int term,string actor)
        {
            if(!Owns(term)||!data.automatic||data.phase!=GameFlowPhase.Playing&&data.phase!=GameFlowPhase.Results||Array.IndexOf(data.humanIds,actor)<0)return false;
            bool changed=Array.IndexOf(data.playlistIds,actor)>=0||Array.IndexOf(data.withdrawnIds,actor)<0;
            if(!changed)return false;
            data.playlistIds=Array.FindAll(data.playlistIds,id=>id!=actor);
            if(Array.IndexOf(data.withdrawnIds,actor)<0){var ids=new List<string>(data.withdrawnIds);ids.Add(actor);data.withdrawnIds=ids.ToArray();}
            Changed();return true;
        }

        public bool PrepareModeStart(int term,string operation)
        {
            if(!Owns(term)||data.pending!=GameFlowEffect.LoadGame||data.phase!=GameFlowPhase.LoadingGame||data.operationId!=operation||data.modeStartOperation==operation)return false;
            data.modeStartOperation=operation;data.modeStartRound=data.round;Changed();return true;
        }

        public bool RejectSetup(int term,string reason)
        {
            if(!Owns(term)||Transitioning||data.phase==GameFlowPhase.Playing||data.phase==GameFlowPhase.Results)return false;
            ClearOperation();ClearReady();data.phase=GameFlowPhase.Failed;data.remaining=0;
            data.error=string.IsNullOrEmpty(reason)?"No compatible prototype profile is available.":reason.Substring(0,Math.Min(240,reason.Length));
            Changed();return true;
        }

        // The adapter authenticates the host request or a supported participant Stay request.
        public bool SetAutomatic(int term, bool automatic)
        {
            if (!Owns(term) || data.demo || Transitioning || data.automatic == automatic) return false;
            data.automatic = automatic;
            if (!automatic && data.phase == GameFlowPhase.Choosing)
            { ClearOperation(); data.phase = GameFlowPhase.Idle; data.remaining = 0; }
            if (automatic && data.phase == GameFlowPhase.Results) data.remaining = data.timings.results;
            Changed(); return true;
        }

        public bool SceneCompleted(int term, string operation, GameFlowEffect destination)
        {
            if (!Owns(term) || !Transitioning || data.operationId != operation || data.pending != destination) return false;
            if (data.phase != GameFlowPhase.LoadingGame && data.phase != GameFlowPhase.LoadingLobby && data.phase != GameFlowPhase.Failed) return false;
            bool game = destination == GameFlowEffect.LoadGame;
            ClearOperation(); data.phase = game ? GameFlowPhase.Playing : data.automatic ? GameFlowPhase.Choosing : GameFlowPhase.Idle;
            data.remaining = !game && data.automatic ? ChooseDelay : 0;
            if (!game) data.profileId = "";
            if (!game) { ClearReady(); data.launchIds=Array.Empty<string>(); }
            Changed(); return true;
        }

        public bool SceneFailed(int term, string operation, int attempt)
        {
            if (!Owns(term) || !Transitioning || data.operationId != operation || data.attempts!=attempt ||
                data.phase!=GameFlowPhase.LoadingGame && data.phase!=GameFlowPhase.LoadingLobby) return false;
            data.phase = GameFlowPhase.Failed; data.remaining = 0;
            data.error = "Could not load the room scene. Retry or leave the room."; Changed(); return true;
        }

        // Adapter first reconciles the actual scene. Retrying keeps the operation ID stable.
        public bool Retry(int term, string operation, int failedAttempt)
        {
            if (!Owns(term) || data.phase != GameFlowPhase.Failed || data.pending==GameFlowEffect.None || data.attempts >= 3 ||
                operation!=data.operationId || failedAttempt!=data.attempts) return false;
            data.attempts++; data.error = "";
            data.remaining = data.pending==GameFlowEffect.ChooseProfile ? data.timings.profileTimeout : data.timings.sceneTimeout;
            data.phase = data.pending == GameFlowEffect.LoadGame ? GameFlowPhase.LoadingGame : data.pending==GameFlowEffect.ReturnLobby ? GameFlowPhase.LoadingLobby : GameFlowPhase.Choosing;
            Changed(); return true;
        }

        public bool RoundEnded(int term, int round)
        {
            if (!Owns(term) || data.phase != GameFlowPhase.Playing || data.round != round) return false;
            data.phase = GameFlowPhase.Results; data.remaining = data.timings.results; Changed(); return true;
        }

        public bool ReturnToLobby(int term)
        {
            if (!Owns(term) || data.demo || data.phase != GameFlowPhase.Idle && data.phase != GameFlowPhase.Results && data.phase != GameFlowPhase.Playing &&
                !(data.phase==GameFlowPhase.Failed&&data.pending==GameFlowEffect.LoadGame)) return false;
            Begin(GameFlowEffect.ReturnLobby, GameFlowPhase.LoadingLobby, data.timings.sceneTimeout); Changed(); return true;
        }

        public bool Cancel(int term)
        {
            if (!Owns(term) || Transitioning || data.phase == GameFlowPhase.Playing || data.phase == GameFlowPhase.Results) return false;
            data.automatic = false; data.startRequested = false; checked { data.selectionVersion++; }
            ClearReady();
            ClearOperation(); data.phase = GameFlowPhase.Idle; data.remaining = 0; Changed(); return true;
        }

        // Only verified new room authority may call this. Scene outbox survives migration.
        public bool AdoptAuthority(int newTerm)
        {
            if (newTerm <= data.authorityTerm) return false;
            data.authorityTerm = newTerm;
            ClearReady(); checked { data.rosterVersion++; }
            if (data.phase == GameFlowPhase.Selected || data.phase == GameFlowPhase.WaitingReady)
            { data.phase = GameFlowPhase.Selected; data.remaining = data.timings.selection; data.startRequested = false; checked { data.selectionVersion++; } }
            Changed(); return true;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Opt-in wire experiment. Every player value is retained at its original precision.
    public static class CompactMatchCodec
    {
        public static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--dj-compact-match") >= 0;
        const uint Magic = 0x31434D44;
        const int MaxTextBytes = 4096;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly FieldInfo[] FrameFields = typeof(MatchFrame).GetFields(BindingFlags.Public | BindingFlags.Instance);
        static readonly string[] PlayerFields = "String:id,String:name,String:role,String:spectating,Int32:team,Int32:rank,Int32:kills,Int32:watching,Int32:deaths,Int32:captures,Int32:flagReturns,Int32:blocksClaimed,Int32:infections,Int32:bodyReports,Int32:revives,Single:hillSeconds,Single:lanternPoints,Int32:steals,Color:color,Color:survivorColor,Boolean:alive,Boolean:bot,Boolean:local,Boolean:dying,Single:deathProgress,Single:deathRemaining,Single:handPowerRemaining,Single:heightObservedSeconds,String:pendingOutcome,String:handReturnRole,Boolean:sinking,Single:sinkProgress,Single:health,Single:metric,Single:survivalSeconds,Single:averageHeight,Single:height,Single:claimProgress,Int32:standingBlock,Int32:blockOwner,Vector3:position,Vector3:viewPosition,Quaternion:viewRotation".Split(',');
        static void CheckSchema()
        {
            var fields = typeof(MatchPlayerFrame).GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (fields.Length != PlayerFields.Length || fields.Any(field => Array.IndexOf(PlayerFields, field.FieldType.Name + ":" + field.Name) < 0))
                throw new InvalidDataException("Compact match player schema changed; update the codec before enabling it");
        }
        static bool schemaChecked;
        public static byte[] Pack(MatchFrame frame)
        {
            if (!schemaChecked) { CheckSchema(); schemaChecked = true; }
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            var metadata = new MatchFrame();
            foreach (var field in FrameFields) field.SetValue(metadata, field.GetValue(frame));
            metadata.players = metadata.topTen = metadata.secondaryLeaders = Array.Empty<MatchPlayerFrame>();
            var json = Utf8.GetBytes(JsonUtility.ToJson(metadata));
            if (json.Length > 512000) throw new InvalidDataException("Match metadata exceeds its bound");
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Utf8, true))
            {
                writer.Write(Magic); writer.Write(json.Length); writer.Write(json);
                WritePlayers(writer, frame.players, 256);
                WritePlayers(writer, frame.topTen, 10);
                WritePlayers(writer, frame.secondaryLeaders, 5);
            }
            return CastleFusionWire.Compress(stream.ToArray());
        }
        public static MatchFrame Unpack(byte[] packet)
        {
            if (!schemaChecked) { CheckSchema(); schemaChecked = true; }
            using var stream = new MemoryStream(CastleFusionWire.Decompress(packet), false);
            using var reader = new BinaryReader(stream, Utf8);
            if (reader.ReadUInt32() != Magic) throw new InvalidDataException("Unknown compact match format");
            int length = reader.ReadInt32();
            if (length < 2 || length > 512000 || length > stream.Length - stream.Position)
                throw new InvalidDataException("Invalid match metadata length");
            var frame = JsonUtility.FromJson<MatchFrame>(Utf8.GetString(reader.ReadBytes(length)));
            if (frame == null) throw new InvalidDataException("Missing match metadata");
            frame.players = ReadPlayers(reader, 256);
            frame.topTen = ReadPlayers(reader, 10);
            frame.secondaryLeaders = ReadPlayers(reader, 5);
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing compact match data");
            return frame;
        }
        static void WriteText(BinaryWriter writer, string value)
        {
            if (value == null) { writer.Write(-1); return; }
            int length = Utf8.GetByteCount(value);
            if (length > MaxTextBytes) throw new InvalidDataException("Match text exceeds its bound");
            writer.Write(length); writer.Write(Utf8.GetBytes(value));
        }
        static string ReadText(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == -1) return null;
            if (length < 0 || length > MaxTextBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid match text length");
            return Utf8.GetString(reader.ReadBytes(length));
        }
        static void WritePlayers(BinaryWriter writer, MatchPlayerFrame[] players, int maximum)
        {
            if (players == null || players.Length > maximum) throw new InvalidDataException("Invalid match player count");
            writer.Write(players.Length);
            foreach (var player in players)
            {
                if (player == null) throw new InvalidDataException("Missing match player");
                WritePlayer(writer, player);
            }
        }
        static MatchPlayerFrame[] ReadPlayers(BinaryReader reader, int maximum)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum) throw new InvalidDataException("Invalid match player count");
            var players = new MatchPlayerFrame[count];
            for (int i = 0; i < count; i++) players[i] = ReadPlayer(reader);
            return players;
        }
        static void WritePlayer(BinaryWriter writer, MatchPlayerFrame value)
        {
            WriteText(writer, value.id);
            WriteText(writer, value.name);
            WriteText(writer, value.role);
            WriteText(writer, value.spectating);
            writer.Write(value.team);
            writer.Write(value.rank);
            writer.Write(value.kills);
            writer.Write(value.watching);
            writer.Write(value.deaths);
            writer.Write(value.captures);
            writer.Write(value.flagReturns);
            writer.Write(value.blocksClaimed);
            writer.Write(value.infections);
            writer.Write(value.bodyReports);
            writer.Write(value.revives);
            writer.Write(value.hillSeconds);
            writer.Write(value.lanternPoints);
            writer.Write(value.steals);
            writer.Write(value.color.r); writer.Write(value.color.g); writer.Write(value.color.b); writer.Write(value.color.a);
            writer.Write(value.survivorColor.r); writer.Write(value.survivorColor.g); writer.Write(value.survivorColor.b); writer.Write(value.survivorColor.a);
            writer.Write(value.alive);
            writer.Write(value.bot);
            writer.Write(value.local);
            writer.Write(value.dying);
            writer.Write(value.deathProgress);
            writer.Write(value.deathRemaining);
            writer.Write(value.handPowerRemaining);
            writer.Write(value.heightObservedSeconds);
            WriteText(writer, value.pendingOutcome);
            WriteText(writer, value.handReturnRole);
            writer.Write(value.sinking);
            writer.Write(value.sinkProgress);
            writer.Write(value.health);
            writer.Write(value.metric);
            writer.Write(value.survivalSeconds);
            writer.Write(value.averageHeight);
            writer.Write(value.height);
            writer.Write(value.claimProgress);
            writer.Write(value.standingBlock);
            writer.Write(value.blockOwner);
            writer.Write(value.position.x); writer.Write(value.position.y); writer.Write(value.position.z);
            writer.Write(value.viewPosition.x); writer.Write(value.viewPosition.y); writer.Write(value.viewPosition.z);
            writer.Write(value.viewRotation.x); writer.Write(value.viewRotation.y); writer.Write(value.viewRotation.z); writer.Write(value.viewRotation.w);
        }
        static MatchPlayerFrame ReadPlayer(BinaryReader reader)
        {
            return new MatchPlayerFrame
            {
                id = ReadText(reader),
                name = ReadText(reader),
                role = ReadText(reader),
                spectating = ReadText(reader),
                team = reader.ReadInt32(),
                rank = reader.ReadInt32(),
                kills = reader.ReadInt32(),
                watching = reader.ReadInt32(),
                deaths = reader.ReadInt32(),
                captures = reader.ReadInt32(),
                flagReturns = reader.ReadInt32(),
                blocksClaimed = reader.ReadInt32(),
                infections = reader.ReadInt32(),
                bodyReports = reader.ReadInt32(),
                revives = reader.ReadInt32(),
                hillSeconds = reader.ReadSingle(),
                lanternPoints = reader.ReadSingle(),
                steals = reader.ReadInt32(),
                color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                survivorColor = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                alive = reader.ReadBoolean(),
                bot = reader.ReadBoolean(),
                local = reader.ReadBoolean(),
                dying = reader.ReadBoolean(),
                deathProgress = reader.ReadSingle(),
                deathRemaining = reader.ReadSingle(),
                handPowerRemaining = reader.ReadSingle(),
                heightObservedSeconds = reader.ReadSingle(),
                pendingOutcome = ReadText(reader),
                handReturnRole = ReadText(reader),
                sinking = reader.ReadBoolean(),
                sinkProgress = reader.ReadSingle(),
                health = reader.ReadSingle(),
                metric = reader.ReadSingle(),
                survivalSeconds = reader.ReadSingle(),
                averageHeight = reader.ReadSingle(),
                height = reader.ReadSingle(),
                claimProgress = reader.ReadSingle(),
                standingBlock = reader.ReadInt32(),
                blockOwner = reader.ReadInt32(),
                position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                viewPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                viewRotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            };
        }
    }
}

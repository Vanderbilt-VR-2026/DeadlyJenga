using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Shared output for the picture rigs (maps, wardrobe):
    //  - every run gets a folder (default Logs/Captures/<time>-<name>) holding its pictures and clips,
    //  - manifest.json lists them (for agents and scripts),
    //  - contact-sheet.png shows all pictures in one image (an agent can read just this one file),
    //  - index.html is a gallery for people: grid sizes, copy buttons, click for full size with zoom.
    // The gallery opens in a browser tab only when asked (-open on the CLI, or the menu toggle).
    // Log lines "CAPTURE GALLERY <path>" / "CAPTURE CONTACT <path>" / "CAPTURE FILE <path>" make results easy to find.
    public sealed class CaptureOutput
    {
        const string OpenPreference = "DeadlyJenga.Capture.OpenGallery";

        [Serializable] public sealed class Item { public string file, label, kind; public int width, height; }
        [Serializable] sealed class Manifest { public string title, created; public List<Item> items = new(); }

        public readonly string Folder;
        readonly Manifest manifest = new();

        public CaptureOutput(string title, string folder = null)
        {
            manifest.title = title;
            manifest.created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string safe = new string(title.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            Folder = Path.GetFullPath(folder ?? Path.Combine("Logs", "Captures", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + safe));
            Directory.CreateDirectory(Folder);
            // Re-opening a folder (for example after Unity reloads between maps) keeps what was already listed.
            string existing = PathFor("manifest.json");
            if (File.Exists(existing))
            {
                var previous = JsonUtility.FromJson<Manifest>(File.ReadAllText(existing));
                if (previous?.items != null) manifest.items.AddRange(previous.items);
            }
        }

        // Saves the list now (without the contact sheet or gallery), so a later run can carry on.
        public void SaveManifest() => File.WriteAllText(PathFor("manifest.json"), JsonUtility.ToJson(manifest, true));

        public string PathFor(string fileName) => Path.Combine(Folder, fileName);

        // Register a file (inside or outside Folder). kind: "image" or "video".
        public void Add(string path, string label, string kind = "image", int width = 0, int height = 0)
        {
            manifest.items.Add(new Item { file = Path.GetFullPath(path).Replace('\\', '/'), label = label, kind = kind, width = width, height = height });
            Debug.Log("CAPTURE FILE " + Path.GetFullPath(path));
        }

        public static bool OpenByDefault
        {
            get => EditorPrefs.GetBool(OpenPreference, true);
            set => EditorPrefs.SetBool(OpenPreference, value);
        }

        [MenuItem("Tools/Deadly Jenga/Captures/Open Gallery After Capture", false, 1)]
        static void ToggleOpen() => OpenByDefault = !OpenByDefault;
        [MenuItem("Tools/Deadly Jenga/Captures/Open Gallery After Capture", true)]
        static bool ToggleOpenCheck() { Menu.SetChecked("Tools/Deadly Jenga/Captures/Open Gallery After Capture", OpenByDefault); return true; }

        [MenuItem("Tools/Deadly Jenga/Captures/Open Captures Folder", false, 2)]
        static void OpenFolder() { var f = Path.GetFullPath(Path.Combine("Logs", "Captures")); Directory.CreateDirectory(f); EditorUtility.RevealInFinder(f); }

        // Writes manifest, contact sheet and gallery. Opens the gallery when open is true.
        public string Finish(bool open)
        {
            File.WriteAllText(PathFor("manifest.json"), JsonUtility.ToJson(manifest, true));
            string contact = WriteContactSheet();
            string gallery = PathFor("index.html");
            File.WriteAllText(gallery, GalleryHtml());
            Debug.Log("CAPTURE GALLERY " + gallery);
            if (contact != null) Debug.Log("CAPTURE CONTACT " + contact);
            if (open) Application.OpenURL("file:///" + gallery.Replace('\\', '/'));
            return gallery;
        }

        string WriteContactSheet()
        {
            var images = manifest.items.Where(i => i.kind == "image" && File.Exists(i.file)).Take(400).ToList();
            foreach (var v in manifest.items.Where(i => i.kind == "video")) { string poster = Poster(v.file); if (File.Exists(poster)) images.Add(new Item { file = poster }); }
            if (images.Count == 0) return null;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(images.Count * 1.5f)), rows = Mathf.CeilToInt(images.Count / (float)cols);
            const int cell = 192;
            var sheet = new Texture2D(cols * cell, rows * cell, TextureFormat.RGB24, false);
            var fill = Enumerable.Repeat(new Color32(36, 28, 52, 255), sheet.width * sheet.height).ToArray();
            sheet.SetPixels32(fill);
            var source = new Texture2D(2, 2);
            for (int i = 0; i < images.Count; i++)
            {
                if (!source.LoadImage(File.ReadAllBytes(images[i].file))) continue;
                var scaled = Scale(source, cell - 4);
                int x = (i % cols) * cell + (cell - scaled.width) / 2, y = (rows - 1 - i / cols) * cell + (cell - scaled.height) / 2;
                var px = scaled.GetPixels();
                for (int p = 0; p < px.Length; p++) { var c = px[p]; px[p] = Color.Lerp(new Color32(36, 28, 52, 255), c, c.a); px[p].a = 1; }
                sheet.SetPixels(x, y, scaled.width, scaled.height, px);
                Object.DestroyImmediate(scaled);
            }
            sheet.Apply();
            string path = PathFor("contact-sheet.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet); Object.DestroyImmediate(source);
            return path;
        }

        static Texture2D Scale(Texture2D source, int box)
        {
            float k = Mathf.Min(box / (float)source.width, box / (float)source.height);
            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(source.height * k));
            var rt = RenderTexture.GetTemporary(w, h, 0);
            Graphics.Blit(source, rt);
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0); result.Apply();
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        public static string Poster(string video) => Path.ChangeExtension(video, ".poster.png");

        string GalleryHtml()
        {
            string Rel(string file)
            {
                string folder = Folder.Replace('\\', '/').TrimEnd('/') + "/";
                return file.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ? file.Substring(folder.Length) : "file:///" + file;
            }
            var items = new StringBuilder();
            foreach (var i in manifest.items)
                items.Append("{src:").Append(Json(Rel(i.file))).Append(",path:").Append(Json(i.file)).Append(",label:").Append(Json(i.label ?? Path.GetFileName(i.file)))
                     .Append(",kind:").Append(Json(i.kind)).Append(",poster:").Append(Json(i.kind == "video" ? Rel(Poster(i.file).Replace('\\', '/')) : "")).Append("},\n");
            return GalleryTemplate.Replace("{{TITLE}}", System.Net.WebUtility.HtmlEncode(manifest.title)).Replace("{{ITEMS}}", items.ToString());
        }

        static string Json(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        // Turns a folder of numbered frames (frame_0000.png ...) into an MP4 with ffmpeg, plus a poster picture.
        public static bool EncodeClip(string framesFolder, int fps, string outputMp4)
        {
            try
            {
                var frames = Directory.GetFiles(framesFolder, "frame_*.png").OrderBy(f => f).ToArray();
                if (frames.Length == 0) return false;
                File.Copy(frames[frames.Length / 2], Poster(outputMp4), true);
                var info = new ProcessStartInfo("ffmpeg", $"-y -loglevel error -framerate {fps} -i \"{Path.Combine(framesFolder, "frame_%04d.png")}\" -c:v libx264 -pix_fmt yuv420p -crf 20 -movflags +faststart \"{outputMp4}\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                using var process = Process.Start(info);
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) { Debug.LogError("ffmpeg failed: " + errors); return false; }
                Directory.Delete(framesFolder, true);
                return true;
            }
            catch (Exception e) { Debug.LogError("Clip encoding needs ffmpeg on PATH (frames kept): " + e.Message); return false; }
        }

        const string GalleryTemplate = @"<!doctype html><html><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>
<title>{{TITLE}}</title><style>
:root{--bg:#1c1530;--card:#2a2045;--ink:#f3eefe;--mute:#b9aed6;--mint:#59ffc4}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:14px system-ui,sans-serif}
header{position:sticky;top:0;z-index:2;display:flex;flex-wrap:wrap;gap:10px;align-items:center;padding:10px 16px;background:#150f25ee;border-bottom:1px solid #3a2f5c}
h1{font-size:16px;margin:0 8px 0 0}button,select,input{background:var(--card);color:var(--ink);border:1px solid #4a3d73;border-radius:8px;padding:6px 10px;font:inherit;cursor:pointer}
button:hover{border-color:var(--mint)}.count{color:var(--mute)}#grid{display:grid;gap:12px;padding:16px;grid-template-columns:repeat(auto-fill,minmax(var(--size,220px),1fr))}
#grid.list{grid-template-columns:1fr}#grid.list .card{display:flex;gap:12px;align-items:center}#grid.list .thumb{width:var(--size,220px);flex:none}
.card{background:var(--card);border-radius:12px;overflow:hidden}.thumb{aspect-ratio:var(--ratio,1);background:#0f0a1c;display:flex;align-items:center;justify-content:center;cursor:zoom-in}
.thumb img,.thumb video{max-width:100%;max-height:100%;object-fit:contain}.meta{padding:8px 10px;display:flex;gap:6px;align-items:center;flex-wrap:wrap}
.meta span{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.meta button{padding:3px 8px;font-size:12px}
#view{position:fixed;inset:0;z-index:5;background:#08050fee;display:none;overflow:hidden;cursor:grab}#view.on{display:block}
#view img,#view video{position:absolute;left:50%;top:50%;transform-origin:center;max-width:none;user-select:none}
#bar{position:fixed;z-index:6;top:10px;right:12px;display:none;gap:8px}#view.on~#bar{display:flex}
#toast{position:fixed;bottom:16px;left:50%;transform:translateX(-50%);background:var(--mint);color:#1c1530;padding:6px 14px;border-radius:999px;opacity:0;transition:.2s}
</style></head><body>
<header><h1>{{TITLE}}</h1><span class=count id=count></span>
<label>Size <input id=size type=range min=96 max=640 value=220></label>
<select id=ratio><option value=1>Square</option><option value=1.4545>Wide</option><option value=1.7778>16:9</option></select>
<button id=layout>List view</button><input id=filter placeholder='Filter by name'>
<button id=copyAll>Copy all paths</button><button id=copyFolder>Copy folder path</button></header>
<div id=grid></div><div id=view></div><div id=bar><button id=prev>&larr;</button><button id=next>&rarr;</button><button id=fit>Fit</button><button id=one>100%</button><button id=copyOne>Copy</button><button id=close>Close (Esc)</button></div><div id=toast></div>
<script>
const items=[{{ITEMS}}];const grid=document.getElementById('grid'),view=document.getElementById('view');let shown=items,current=-1,zoom=1,px=0,py=0;
const toast=t=>{const e=document.getElementById('toast');e.textContent=t;e.style.opacity=1;setTimeout(()=>e.style.opacity=0,1200)};
async function copyItem(it){try{if(it.kind=='image'){const b=await (await fetch(it.src)).blob();await navigator.clipboard.write([new ClipboardItem({[b.type||'image/png']:b})]);toast('Image copied');return}}catch(e){}
 await navigator.clipboard.writeText(it.path);toast('Path copied')}
function render(){const f=document.getElementById('filter').value.toLowerCase();shown=items.filter(i=>i.label.toLowerCase().includes(f));
 document.getElementById('count').textContent=shown.length+' of '+items.length;grid.innerHTML='';
 shown.forEach((it,n)=>{const c=document.createElement('div');c.className='card';const m=it.kind=='video'?`<video src='${it.src}' poster='${it.poster}' muted loop playsinline onmouseenter='this.play()' onmouseleave='this.pause()'></video>`:`<img loading=lazy src='${it.src}'>`;
  c.innerHTML=`<div class=thumb>${m}</div><div class=meta><span title='${it.label}'>${it.label}</span><button>Copy</button><button>Path</button></div>`;
  c.querySelector('.thumb').onclick=()=>open(n);const b=c.querySelectorAll('button');b[0].onclick=()=>copyItem(it);b[1].onclick=()=>{navigator.clipboard.writeText(it.path);toast('Path copied')};grid.appendChild(c)})}
function place(){const el=view.firstChild;if(el)el.style.transform=`translate(calc(-50% + ${px}px),calc(-50% + ${py}px)) scale(${zoom})`}
function fit(){const el=view.firstChild;if(!el)return;const w=el.naturalWidth||el.videoWidth||1,h=el.naturalHeight||el.videoHeight||1;zoom=Math.min(innerWidth/w,innerHeight/h)*.95;px=py=0;place()}
function open(n){current=(n+shown.length)%shown.length;const it=shown[current];view.innerHTML=it.kind=='video'?`<video src='${it.src}' controls autoplay loop></video>`:`<img src='${it.src}'>`;
 const el=view.firstChild;el.onload=el.onloadedmetadata=fit;view.classList.add('on')}
function close(){view.classList.remove('on');view.innerHTML='';current=-1}
view.onwheel=e=>{e.preventDefault();zoom*=e.deltaY<0?1.15:1/1.15;place()};let drag=null;
view.onmousedown=e=>{if(e.target.tagName=='VIDEO')return;drag=[e.clientX-px,e.clientY-py]};onmousemove=e=>{if(drag){px=e.clientX-drag[0];py=e.clientY-drag[1];place()}};onmouseup=()=>drag=null;
view.ondblclick=()=>{zoom=zoom<1.5?2.5:1;place()};
onkeydown=e=>{if(current<0)return;if(e.key=='Escape')close();if(e.key=='ArrowRight')open(current+1);if(e.key=='ArrowLeft')open(current-1)};
document.getElementById('close').onclick=close;document.getElementById('prev').onclick=()=>open(current-1);document.getElementById('next').onclick=()=>open(current+1);
document.getElementById('fit').onclick=fit;document.getElementById('one').onclick=()=>{zoom=1;px=py=0;place()};document.getElementById('copyOne').onclick=()=>copyItem(shown[current]);
document.getElementById('size').oninput=e=>grid.style.setProperty('--size',e.target.value+'px');document.getElementById('ratio').onchange=e=>grid.style.setProperty('--ratio',e.target.value);
document.getElementById('layout').onclick=e=>{grid.classList.toggle('list');e.target.textContent=grid.classList.contains('list')?'Grid view':'List view'};
document.getElementById('filter').oninput=render;document.getElementById('copyAll').onclick=()=>{navigator.clipboard.writeText(shown.map(i=>i.path).join('\n'));toast(shown.length+' paths copied')};
document.getElementById('copyFolder').onclick=()=>{const p=items.length?items[0].path.replace(/\/[^\/]*$/,''):'';navigator.clipboard.writeText(p);toast('Folder copied')};render();
</script></body></html>";
    }

    // Moving-camera shots for clips. t runs 0..1 over the clip.
    public static class CameraPath
    {
        public static readonly string[] Modes = { "orbit", "arc", "dolly", "pan", "crane" };

        // baseDirection: from focus toward the camera at the start of the shot.
        public static void Evaluate(string mode, float t, Vector3 focus, float distance, Vector3 baseDirection, out Vector3 position, out Quaternion rotation)
        {
            float e = Mathf.SmoothStep(0, 1, t);
            var flat = new Vector3(baseDirection.x, 0, baseDirection.z).normalized;
            if (flat.sqrMagnitude < .01f) flat = Vector3.back;
            float height = baseDirection.normalized.y;
            Vector3 dir;
            switch (mode)
            {
                case "orbit": // full circle at a steady speed
                    dir = Quaternion.Euler(0, 360f * t, 0) * flat * Mathf.Sqrt(1 - height * height) + Vector3.up * height;
                    position = focus + dir.normalized * distance; break;
                case "arc": // 100 degree sweep, eased
                    dir = Quaternion.Euler(0, Mathf.Lerp(-50, 50, e), 0) * flat * Mathf.Sqrt(1 - height * height) + Vector3.up * height;
                    position = focus + dir.normalized * distance; break;
                case "dolly": // straight push in
                    position = focus + baseDirection.normalized * Mathf.Lerp(distance * 1.25f, distance * .6f, e); break;
                case "pan": // straight slide sideways, always looking at the subject
                    var side = Vector3.Cross(Vector3.up, flat).normalized;
                    position = focus + baseDirection.normalized * distance + side * Mathf.Lerp(-.45f, .45f, e) * distance; break;
                default: // crane: rise from low to high
                    dir = flat * Mathf.Cos(Mathf.Lerp(.1f, 1.1f, e)) + Vector3.up * Mathf.Sin(Mathf.Lerp(.1f, 1.1f, e));
                    position = focus + dir.normalized * distance; break;
            }
            rotation = Quaternion.LookRotation(focus - position);
        }
    }
}

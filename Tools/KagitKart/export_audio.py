"""Render the pinned original WebAudio recipes; no Python or browser audio dependency."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

REVISION = "da100b21ae1c5332d2843d21a9bb7b68b97e9c2a"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--checks-only", action="store_true", help="Refresh native WebAudio regression fixtures without re-rendering songs")
    parser.add_argument("--reference-dir", type=Path, help="Optional fixture folder; defaults to Tests/EditMode/Demo/KagitKart/Fixtures")
    parser.add_argument("--edge", type=Path, default=Path(os.environ.get("PROGRAMFILES(X86)", "C:/Program Files (x86)")) / "Microsoft/Edge/Application/msedge.exe")
    args = parser.parse_args()
    revision = subprocess.check_output(["git", "-C", str(args.source), "rev-parse", "HEAD"], text=True).strip()
    if revision != REVISION:
        raise SystemExit(f"Expected source revision {REVISION}, found {revision}")
    if subprocess.call(["git", "-C", str(args.source), "diff", "--quiet", "HEAD", "--", "src/audio", "src/track/defs"]):
        raise SystemExit("Original audio/track source has uncommitted edits; export requires the pinned source.")
    project = Path(__file__).resolve().parents[2]
    destination = project / "Assets/LoadResources/Demos/kagit_kart"
    destination.mkdir(parents=True, exist_ok=True)
    done = threading.Event()
    failure = []

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path == "/" or self.path == "/?checks":
                payload = b'<script type="module" src="/export_audio.mjs"></script>'
                content_type = "text/html"
            elif self.path == "/export_audio.mjs":
                payload = Path(__file__).with_suffix(".mjs").read_bytes()
                content_type = "application/javascript"
            elif self.path.startswith("/src/") and ".." not in self.path:
                payload = (args.source / self.path.lstrip("/")).read_bytes()
                content_type = "application/javascript"
            elif self.path.startswith("/engine-"):
                layer = self.path.split("-")[1].split(".")[0]
                source = (args.source / "src/audio/engine.js").read_text(encoding="utf-8")
                names = ["body", "flutter", "rattle", "squeak", "whine", "roar", "wind", "shimmer"]
                keep = {"drift": {"squeak", "whine"}}.get(layer, {layer})
                muted = [name + ".gain" for name in names if name not in keep]
                source = source.replace("param.setTargetAtTime(v, now, tc);", f"if ([{','.join(muted)}].includes(param)) v = 0; param.setTargetAtTime(v, now, tc);")
                if layer == "body" or layer == "cpu":
                    source = source.replace("set(lp.frequency, 300 +", "set(lp.frequency, 20000 + 0 *")
                    source = source.replace("set(v.f.frequency, 500 +", "set(v.f.frequency, 20000 + 0 *")
                payload = source.encode("utf-8")
                content_type = "application/javascript"
            else:
                self.send_error(404)
                return
            self.send_response(200)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)

        def do_POST(self):
            payload = self.rfile.read(int(self.headers["Content-Length"]))
            name = self.path.lstrip("/")
            if name == "error":
                failure.append(payload.decode())
                done.set()
            elif name == "done":
                done.set()
            elif name == "reference.json":
                reference_dir = args.reference_dir or project / "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures"
                reference_dir.mkdir(parents=True, exist_ok=True)
                (reference_dir / "OriginalAudioDynamics.json").write_bytes(payload)
            elif name == "boost-reference.json":
                reference_dir = args.reference_dir or project / "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures"
                reference_dir.mkdir(parents=True, exist_ok=True)
                (reference_dir / "OriginalAudioBoost.json").write_bytes(payload)
            elif name == "manifest.json":
                manifest = json.loads(payload)
                manifest["sourceRevision"] = REVISION
                (destination / "Data/AudioManifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
                done.set()
            elif name.endswith(".wav") and Path(name).name == name:
                segments = Path(name).stem.split("_")
                semantic = "_".join(part.zfill(2) if part.isdigit() else part[0].upper() + part[1:] for part in segments)
                group = {"music": "Music", "sting": "Stingers", "engine": "Engines"}.get(segments[0], "")
                target = destination / "Audio/SFX" / group / (semantic + ".wav")
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(payload)
                print(name, flush=True)
            else:
                self.send_error(400)
                return
            self.send_response(200)
            self.end_headers()

        def log_message(self, *_):
            pass

    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    with tempfile.TemporaryDirectory(prefix="kagit-audio-") as profile:
        browser = subprocess.Popen([str(args.edge), "--headless=new", "--disable-gpu", "--no-first-run", "--disable-features=msEdgeSidebarV2", f"--user-data-dir={profile}", f"http://127.0.0.1:{server.server_port}/" + ("?checks" if args.checks_only else "")], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        try:
            if not done.wait(1200):
                raise TimeoutError("Original audio rendering did not complete in 20 minutes")
            if failure:
                raise RuntimeError(failure[0])
        finally:
            browser.terminate()
            browser.wait(timeout=20)
            server.shutdown()
    print("Original audio export complete.")


if __name__ == "__main__":
    main()

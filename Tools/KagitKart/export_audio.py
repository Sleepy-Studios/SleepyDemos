"""Render the pinned WebAudio recipes; store music as compact Ogg Vorbis sources."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

REVISION = "da100b21ae1c5332d2843d21a9bb7b68b97e9c2a"


def write_music(target, payload, ffmpeg):
    # 先完成编码，再覆盖资产；音乐保持原采样率、声道和精确循环长度。
    with tempfile.TemporaryDirectory(prefix="kagit-music-") as directory:
        source = Path(directory) / "source.wav"
        encoded = Path(directory) / "music.ogg"
        source.write_bytes(payload)
        subprocess.run([str(ffmpeg), "-hide_banner", "-loglevel", "error", "-i", str(source),
                        "-c:a", "libvorbis", "-q:a", "7", str(encoded)], check=True,
                       creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        target.write_bytes(encoded.read_bytes())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--checks-only", action="store_true", help="Refresh native WebAudio regression fixtures without re-rendering songs")
    parser.add_argument("--reference-dir", type=Path, help="Optional fixture folder; defaults to Tests/EditMode/Demo/KagitKart/Fixtures")
    parser.add_argument("--output-dir", type=Path, help="Optional staging folder; defaults to the Demo resource folder")
    parser.add_argument("--ffmpeg", type=Path, default=shutil.which("ffmpeg"), help="FFmpeg with libvorbis; required for music export")
    parser.add_argument("--edge", type=Path, default=Path(os.environ.get("PROGRAMFILES(X86)", "C:/Program Files (x86)")) / "Microsoft/Edge/Application/msedge.exe")
    args = parser.parse_args()
    if not args.checks_only and (args.ffmpeg is None or not args.ffmpeg.is_file()):
        parser.error("Music export requires FFmpeg: put it on PATH or supply --ffmpeg.")
    revision = subprocess.check_output(["git", "-C", str(args.source), "rev-parse", "HEAD"], text=True).strip()
    if revision != REVISION:
        raise SystemExit(f"Expected source revision {REVISION}, found {revision}")
    if subprocess.call(["git", "-C", str(args.source), "diff", "--quiet", "HEAD", "--", "src/audio", "src/track/defs"]):
        raise SystemExit("Original audio/track source has uncommitted edits; export requires the pinned source.")
    project = Path(__file__).resolve().parents[2]
    destination = args.output_dir or project / "Assets/LoadResources/Demos/kagit_kart"
    destination.mkdir(parents=True, exist_ok=True)
    (destination / "Data").mkdir(exist_ok=True)
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
                if self.path == "/src/audio/mixer.js":
                    # Save the music before its live duck filter. Runtime applies that filter once.
                    payload = payload.replace(b"musicIn.connect(duckFilter)", b"musicIn.connect(duck)")
                    payload = payload.replace(b"reverbOut.connect(duckFilter)", b"reverbOut.connect(duck)")
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
            elif name == "shaper-reference.json":
                reference_dir = args.reference_dir or project / "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures"
                reference_dir.mkdir(parents=True, exist_ok=True)
                (reference_dir / "OriginalEngineSoftClip.json").write_bytes(payload)
            elif name == "manifest.json":
                manifest = json.loads(payload)
                manifest["sourceRevision"] = REVISION
                (destination / "Data/AudioManifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
                done.set()
            elif name.endswith(".wav") and Path(name).name == name:
                segments = Path(name).stem.split("_")
                semantic = "_".join(part.zfill(2) if part.isdigit() else part[0].upper() + part[1:] for part in segments)
                group = {"music": "Music", "sting": "Stingers"}.get(segments[0], "")
                target = destination / "Audio/SFX" / group / (semantic + (".ogg" if group == "Music" else ".wav"))
                target.parent.mkdir(parents=True, exist_ok=True)
                try:
                    if group == "Music":
                        write_music(target, payload, args.ffmpeg)
                    else:
                        target.write_bytes(payload)
                except Exception as error:
                    failure.append(f"{name}: {error}")
                    done.set()
                    self.send_error(500)
                    return
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

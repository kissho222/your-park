from __future__ import annotations

import argparse
import json
import math
import tempfile
import time
from pathlib import Path

import torch
import torchaudio as ta
from chatterbox.vc import ChatterboxVC


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="YourPark ChatterboxVC PoC")
    p.add_argument("--source", type=Path, required=True, help="Source speech WAV")
    p.add_argument("--target", type=Path, required=True, help="Target/reference speaker WAV")
    p.add_argument("--output", type=Path, default=Path("converted.wav"))
    p.add_argument(
        "--reference-seconds",
        type=float,
        nargs="+",
        default=[10.0],
        help="Reference lengths to compare (default: 10)",
    )
    p.add_argument(
        "--device",
        choices=["auto", "cuda", "cpu", "mps"],
        default="auto",
    )
    return p.parse_args()


def choose_device(requested: str) -> str:
    if requested != "auto":
        if requested == "cuda" and not torch.cuda.is_available():
            raise RuntimeError("CUDA was requested but torch.cuda.is_available() is false")
        if requested == "mps" and not torch.backends.mps.is_available():
            raise RuntimeError("MPS was requested but is unavailable")
        return requested
    if torch.cuda.is_available():
        return "cuda"
    if torch.backends.mps.is_available():
        return "mps"
    return "cpu"


def validate_audio(path: Path, label: str) -> tuple[int, int, float]:
    if not path.is_file():
        raise FileNotFoundError(f"{label} not found: {path}")
    info = ta.info(str(path))
    if info.num_frames <= 0 or info.sample_rate <= 0:
        raise ValueError(f"{label} has no usable audio: {path}")
    duration = info.num_frames / info.sample_rate
    return info.sample_rate, info.num_channels, duration


def make_reference_clip(src: Path, seconds: float, tmp_dir: Path) -> tuple[Path, float]:
    if not math.isfinite(seconds) or seconds <= 0:
        raise ValueError(f"Invalid reference length: {seconds}")
    wav, sr = ta.load(str(src))
    max_frames = max(1, int(round(seconds * sr)))
    wav = wav[:, :max_frames]
    actual_seconds = wav.shape[1] / sr
    out = tmp_dir / f"target_ref_{seconds:g}s.wav"
    ta.save(str(out), wav, sr)
    return out, actual_seconds


def output_for_reference(base: Path, seconds: float, multiple: bool) -> Path:
    if not multiple:
        return base
    suffix = base.suffix or ".wav"
    return base.with_name(f"{base.stem}_ref{seconds:g}s{suffix}")


def report_path_for(output: Path) -> Path:
    return output.with_suffix(".report.json")


def main() -> int:
    args = parse_args()
    source_sr, source_channels, source_duration = validate_audio(args.source, "source")
    target_sr, target_channels, target_duration = validate_audio(args.target, "target")

    device = choose_device(args.device)
    print(f"Device: {device}")
    if device == "cuda":
        print(f"GPU: {torch.cuda.get_device_name(0)}")

    load_start = time.perf_counter()
    model = ChatterboxVC.from_pretrained(device)
    model_load_seconds = time.perf_counter() - load_start
    print(f"Model loaded in {model_load_seconds:.3f}s")

    refs = []
    seen = set()
    for value in args.reference_seconds:
        value = float(value)
        if value not in seen:
            refs.append(value)
            seen.add(value)

    multiple = len(refs) > 1
    args.output.parent.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="yourpark-vc-") as tmp:
        tmp_dir = Path(tmp)
        for ref_seconds in refs:
            ref_path, actual_ref_seconds = make_reference_clip(args.target, ref_seconds, tmp_dir)
            output = output_for_reference(args.output, ref_seconds, multiple)

            if device == "cuda":
                torch.cuda.empty_cache()
                torch.cuda.reset_peak_memory_stats()
                torch.cuda.synchronize()

            started = time.perf_counter()
            converted = model.generate(
                audio=str(args.source),
                target_voice_path=str(ref_path),
            )
            if device == "cuda":
                torch.cuda.synchronize()
            conversion_seconds = time.perf_counter() - started

            ta.save(str(output), converted, model.sr)
            output_frames = int(converted.shape[-1])
            output_duration = output_frames / model.sr
            rtf = conversion_seconds / source_duration if source_duration > 0 else None

            peak_allocated = None
            peak_reserved = None
            if device == "cuda":
                peak_allocated = int(torch.cuda.max_memory_allocated())
                peak_reserved = int(torch.cuda.max_memory_reserved())

            report = {
                "engine": "ResembleAI ChatterboxVC",
                "source": str(args.source.resolve()),
                "target": str(args.target.resolve()),
                "output": str(output.resolve()),
                "device": device,
                "gpu": torch.cuda.get_device_name(0) if device == "cuda" else None,
                "torch_version": torch.__version__,
                "model_load_seconds": model_load_seconds,
                "conversion_seconds": conversion_seconds,
                "source_sample_rate": source_sr,
                "source_channels": source_channels,
                "source_duration_seconds": source_duration,
                "target_sample_rate": target_sr,
                "target_channels": target_channels,
                "target_duration_seconds": target_duration,
                "requested_reference_seconds": ref_seconds,
                "actual_reference_seconds": actual_ref_seconds,
                "output_sample_rate": model.sr,
                "output_duration_seconds": output_duration,
                "real_time_factor": rtf,
                "cuda_peak_memory_allocated_bytes": peak_allocated,
                "cuda_peak_memory_reserved_bytes": peak_reserved,
            }

            report_path = report_path_for(output)
            report_path.write_text(
                json.dumps(report, ensure_ascii=False, indent=2),
                encoding="utf-8",
            )

            print(
                f"ref={actual_ref_seconds:.2f}s -> {output} | "
                f"convert={conversion_seconds:.3f}s | RTF={rtf:.3f}"
                if rtf is not None
                else f"ref={actual_ref_seconds:.2f}s -> {output} | convert={conversion_seconds:.3f}s"
            )
            if peak_reserved is not None:
                print(
                    f"  CUDA peak allocated={peak_allocated / (1024**3):.2f} GiB, "
                    f"reserved={peak_reserved / (1024**3):.2f} GiB"
                )
            print(f"  report: {report_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
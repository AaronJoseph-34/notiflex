"""Prepare pinned local speech models. Requires numpy, onnx and onnxruntime.

No microphone data or cloud inference is used. Generated ONNX files go into an
Editor-only staging folder, then Unity serializes them through SpeechModelBuilder.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

import numpy as np
import onnx
import onnxruntime as ort
from onnx import numpy_helper

SILERO_REV = "066409f3d9db45a30d21dfcbf9147826ffdd7052"
SPEAKER_SHA = "5ef208a9da1453335308a6b6f4e6dfbd7e183a38b604de0a57664f45d257fe94"
SILERO_SHA = "7ed98ddbad84ccac4cd0aeb3099049280713df825c610a8ed34543318f1b2c49"


def fetch(url, target):
    if not target.exists():
        subprocess.run(["curl", "-fLsS", "--max-time", "60", "-o", str(target), url], check=True)
    return target.read_bytes()


def specialize(original, target, shapes, constants=None):
    model = onnx.load(original)
    for name, value in (constants or {}).items():
        model.graph.input.remove(next(x for x in model.graph.input if x.name == name))
        model.graph.initializer.append(numpy_helper.from_array(value, name))
    for value in model.graph.input:
        for dim, size in zip(value.type.tensor_type.shape.dim, shapes[value.name]):
            dim.ClearField("dim_param")
            dim.dim_value = size
    temporary = target.with_suffix(".static.onnx")
    onnx.save(model, temporary)
    options = ort.SessionOptions()
    options.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_BASIC
    options.optimized_model_filepath = str(target)
    ort.InferenceSession(str(temporary), options, providers=["CPUExecutionProvider"])
    temporary.unlink()
    return ort.InferenceSession(str(target), providers=["CPUExecutionProvider"])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--unity-project", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    args.cache.mkdir(parents=True, exist_ok=True)
    stage = args.unity_project / "Assets/Notiflex/Editor/SpeechModelImports"
    stage.mkdir(parents=True, exist_ok=True)
    licenses = args.unity_project / "Assets/Resources/Notiflex"
    licenses.mkdir(parents=True, exist_ok=True)
    raw = args.cache / "silero.onnx"
    digest = hashlib.sha256(fetch(f"https://raw.githubusercontent.com/snakers4/silero-vad/{SILERO_REV}/src/silero_vad/data/silero_vad_16k_op15.onnx", raw)).hexdigest()
    if digest != SILERO_SHA:
        raise ValueError("Silero source digest mismatch")
    silero = specialize(raw, stage / "VoiceActivity.onnx", {"input": [1, 576], "state": [2, 1, 128]}, {"sr": np.array(16000, dtype=np.int64)})
    upstream = ort.InferenceSession(str(raw), providers=["CPUExecutionProvider"])
    rng = np.random.default_rng(60)
    state = np.zeros((2, 1, 128), np.float32)
    error = 0.0
    for i in range(20):
        waveform = np.zeros((1, 576), np.float32) if i == 0 else rng.normal(0, .02, (1, 576)).astype(np.float32)
        a = upstream.run(None, {"input": waveform, "state": state, "sr": np.array(16000, dtype=np.int64)})
        b = silero.run(None, {"input": waveform, "state": state})
        error = max(error, *(float(np.max(np.abs(x-y))) for x, y in zip(a, b)))
        state = a[1]
    if error > 1e-5:
        raise ValueError("Speech specialization changed predictions")
    speaker_path = args.cache / "speaker.onnx"
    speaker_url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-recongition-models/wespeaker_en_voxceleb_resnet34.onnx"
    speaker_data = fetch(speaker_url, speaker_path)
    speaker_digest = hashlib.sha256(speaker_data).hexdigest()
    if speaker_digest != SPEAKER_SHA:
        raise ValueError("Speaker source digest mismatch")
    # The release asset is recorded by digest, and all later reuses are checked
    # against this manifest. Never silently replace a vendored model.
    manifest_path = args.evidence / "speech-model-conversion.json"
    if manifest_path.exists():
        old = json.loads(manifest_path.read_text())
        if old["speakerSourceSha256"] != speaker_digest:
            raise ValueError("Speaker model source changed")
    speaker = specialize(speaker_path, stage / "SpeakerEmbedding.onnx", {"feats": [1, 200, 80]})
    reference = ort.InferenceSession(str(speaker_path), providers=["CPUExecutionProvider"])
    features = rng.normal(0, 1, (1, 200, 80)).astype(np.float32)
    a = reference.run(None, {"feats": features})[0]
    b = speaker.run(None, {"feats": features})[0]
    speaker_error = float(np.max(np.abs(a-b)))
    if speaker_error > 1e-4:
        raise ValueError("Speaker specialization changed predictions")
    for name, url in [
        ("Silero-LICENSE.txt", f"https://raw.githubusercontent.com/snakers4/silero-vad/{SILERO_REV}/LICENSE"),
        ("WeSpeaker-LICENSE.txt", "https://raw.githubusercontent.com/wenet-e2e/wespeaker/45941e7cba2c3ea99e232d02bedf617fc71b0dad/LICENSE"),
    ]:
        fetch(url, licenses / name)
    (licenses / "SpeechModels-NOTICE.txt").write_text(
        "Local speech processing: Silero VAD (MIT), snakers4/silero-vad, revision " + SILERO_REV +
        ". Fixed 16 kHz/576-sample specialization preserves the upstream prediction.\n"
        "Anonymous speaker embeddings: WeSpeaker English VoxCeleb ResNet34, distributed by k2-fsa/sherpa-onnx. "
        "WeSpeaker code license: Apache-2.0; see accompanying license and source provenance. "
        "No named identity recognition, saved voice enrollment, speech transcript or cloud inference.\n"
    )
    args.evidence.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps({
        "sileroRevision": SILERO_REV, "sileroSourceSha256": digest,
        "speakerSourceUrl": speaker_url, "speakerSourceSha256": speaker_digest,
        "sileroDifferentialMaxAbsError": error, "speakerDifferentialMaxAbsError": speaker_error,
        "testInputs": "Synthetic silence/noise/features, no participant recordings",
        "conversion": "Fixed shapes, 16 kHz constant, ORT basic constant folding; no learned weights changed",
        "generatedSha256": {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in stage.glob("*.onnx")},
        "numpyVersion": np.__version__, "onnxVersion": onnx.__version__, "ortVersion": ort.__version__,
    }, indent=2) + "\n")
    print(manifest_path)


if __name__ == "__main__":
    main()

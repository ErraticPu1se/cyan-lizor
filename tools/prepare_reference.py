"""Read a locally installed Rain World into a git-ignored preview cache.

No game DLLs are copied. Only the required atlas regions are decoded.
Usage: python tools/prepare_reference.py --game ".../Rain World"
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import types

# UnityPy eagerly imports its audio exporter when decoding textures. This tool
# is texture-only: leave that exporter unavailable instead of packaging an
# unrelated native audio runtime. Keep this boundary covered by the EXE smoke test.
audio_stub = types.ModuleType("UnityPy.export.AudioClipConverter")
def audio_not_supported(*args, **kwargs):
    raise RuntimeError("Audio extraction is outside this texture-only importer.")
audio_stub.extract_audioclip_samples = audio_not_supported
sys.modules["UnityPy.export.AudioClipConverter"] = audio_stub

import UnityPy
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--game", required=True, type=Path)
    parser.add_argument("--output", type=Path,
                        default=Path(__file__).resolve().parents[1] / "local-data")
    args = parser.parse_args()
    game = args.game.resolve()
    data = game / "RainWorld_Data"
    assembly = data / "Managed" / "Assembly-CSharp.dll"
    if not assembly.is_file():
        parser.error("This folder does not contain RainWorld_Data/Managed/Assembly-CSharp.dll")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    env = UnityPy.load(str(data / "resources.assets"))
    atlas_json = {}
    atlas_images = {}
    for obj in env.objects:
        if obj.type.name not in ("TextAsset", "Texture2D"):
            continue
        item = obj.read()
        if item.m_Name not in ("rainWorld", "rainworldmsc"):
            continue
        if obj.type.name == "TextAsset":
            atlas_json[item.m_Name] = json.loads(item.m_Script)
        else:
            atlas_images[item.m_Name] = item.image
    entries = {}
    for atlas_name, atlas in atlas_json.items():
        for filename, record in atlas["frames"].items():
            name = filename.removesuffix(".png")
            if not (name.startswith("Lizard") or name in ("Circle20", "tinyStar", "pixel")):
                continue
            if record["rotated"]:
                raise ValueError(f"Rotated atlas element is not supported: {name}")
            rect = record["frame"]
            trim = record["spriteSourceSize"]
            source = record["sourceSize"]
            image = atlas_images[atlas_name].crop((rect["x"], rect["y"],
                                                 rect["x"] + rect["w"], rect["y"] + rect["h"]))
            image.save(output / f"{name}.png")
            entries[name] = dict(name=name, width=source["w"], height=source["h"],
                                 x=trim["x"], y=trim["y"], w=trim["w"], h=trim["h"])
    Image.new("RGBA", (1, 1), "white").save(output / "Futile_White.png")
    entries["Futile_White"] = dict(name="Futile_White", width=1, height=1, x=0, y=0, w=1, h=1)
    needed = ["LizardHead0.0", "LizardArm_01", "LizardScaleA0", "Circle20", "tinyStar", "pixel"]
    missing = [n for n in needed if n not in entries]
    if missing:
        raise ValueError(f"Required atlas elements missing: {missing}")
    manifest = dict(schema=1, gamePath=str(game),
                    gameVersion=(data / "StreamingAssets" / "GameVersion.txt").read_text().strip(),
                    assemblySha256=hashlib.sha256(assembly.read_bytes()).hexdigest().upper(),
                    elements=list(entries.values()))
    (output / "reference.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Prepared {len(entries)} atlas elements in {output}")
    print(f"Reference: {manifest['gameVersion']} / {manifest['assemblySha256']}")


if __name__ == "__main__":
    main()

"""Collect installed importer dependency license files into the build directory."""
import importlib.metadata as metadata
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1] / 'Builds' / 'ImporterNotices'
root.mkdir(parents=True, exist_ok=True)
summary = ['Cyan Lizor reference importer: third-party components', '']
for dist in metadata.distributions():
    name = dist.metadata['Name']
    version = dist.version
    summary.append(f'{name} {version}: {dist.metadata.get("License-Expression", dist.metadata.get("License", "See component notices"))}')
    for file in dist.files or []:
        if any(token in str(file).lower() for token in ('license', 'copying', 'notice')):
            source = Path(dist.locate_file(file))
            if source.is_file() and '.dist-info' in str(file):
                target = root / name / str(file).split('.dist-info/', 1)[-1]
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(source.read_bytes())
python_license = Path(sys.base_prefix) / 'LICENSE.txt'
if python_license.exists():
    (root / 'Python-LICENSE.txt').write_bytes(python_license.read_bytes())
(root / 'COMPONENTS.txt').write_text('\n'.join(summary), encoding='utf-8')
print(root)

import sys
from pathlib import Path


WRITER_ROOT = Path(__file__).resolve().parents[1]
if str(WRITER_ROOT) not in sys.path:
    sys.path.insert(0, str(WRITER_ROOT))

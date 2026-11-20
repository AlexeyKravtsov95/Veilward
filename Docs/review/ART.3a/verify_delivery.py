"""Текущая R2; исторический R1 verifier сохранён в git31369a2."""
import runpy
from pathlib import Path
runpy.run_path(str(Path(__file__).resolve().parent/"revision-2/verify_delivery.py"), run_name="__main__")

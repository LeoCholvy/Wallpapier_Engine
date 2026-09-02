import yaml
import os
from pathlib import Path

# Chargement du fichier YAML
CONFIG_FILE = Path(__file__).parent.parent / "config.yaml"
with open(CONFIG_FILE, "r") as f:
    config = yaml.safe_load(f)

PIN_CODE = str(config.get("PIN_CODE", "1234"))
USB_MOUNT_PATH = Path(config.get("USB_MOUNT_PATH", "./usb_mount"))
MAX_RESOLUTION_ALLOWED = config.get("MAX_RESOLUTION_ALLOWED", "QHD")
PORT = int(config.get("PORT", 8000))

# Création des dossiers sur la clé USB (Montage Docker)
DB_PATH = USB_MOUNT_PATH / "database.sqlite"
IMG_PATH = USB_MOUNT_PATH / "img"

USB_MOUNT_PATH.mkdir(parents=True, exist_ok=True)
IMG_PATH.mkdir(parents=True, exist_ok=True)
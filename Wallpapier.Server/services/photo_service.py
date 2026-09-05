import uuid6
import shutil
from pathlib import Path
from sqlalchemy.orm import Session
from fastapi import UploadFile
from typing import Optional
from datetime import datetime
from PIL import Image

from configs.settings import IMG_PATH
from models.schemas import ServerPhoto, ServerSystemState
from services.time_utils import get_paris_now


def save_photo(db: Session, file: UploadFile, location: Optional[str], capture_date: Optional[datetime]) -> ServerPhoto:
    new_id = str(uuid6.uuid7())
    filename = f"{new_id}.jpg"
    thumb_filename = f"{new_id}_thumb.jpg"

    file_path = IMG_PATH / filename
    thumb_path = IMG_PATH / thumb_filename

    # Sauvegarde de l'image originale
    with open(file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

    # Génération de la miniature (très rapide, garde les proportions, max 400x400)
    with Image.open(file_path) as img:
        img.thumbnail((400, 400))
        img.save(thumb_path, "JPEG", quality=75)

    now = get_paris_now()

    db_photo = ServerPhoto(
        id=new_id,
        filename=filename,
        is_favorite=False,
        capture_date=capture_date,
        location=location,
        upload_date=now
    )
    db.add(db_photo)

    update_system_state(db, "last_manifest_update", now.isoformat())
    db.commit()
    db.refresh(db_photo)
    return db_photo


def get_photo_path(photo_id: str) -> Path:
    return IMG_PATH / f"{photo_id}.jpg"


def get_thumb_path(photo_id: str) -> Path:
    return IMG_PATH / f"{photo_id}_thumb.jpg"


def update_favorite(db: Session, photo_id: str, is_favorite: bool):
    photo = db.query(ServerPhoto).filter(ServerPhoto.id == photo_id).first()
    if photo:
        photo.is_favorite = is_favorite
        now = get_paris_now()
        update_system_state(db, f"fav_changed_{photo_id}", now.isoformat())
        update_system_state(db, "last_manifest_update", now.isoformat())
        db.commit()
    return photo


def update_system_state(db: Session, key: str, value: str):
    state = db.query(ServerSystemState).filter(ServerSystemState.key == key).first()
    if state:
        state.value = value
    else:
        db.add(ServerSystemState(key=key, value=value))
import uuid6
import shutil
from pathlib import Path
from sqlalchemy.orm import Session
from fastapi import UploadFile
from typing import Optional
from datetime import datetime

from configs.settings import IMG_PATH
from models.schemas import ServerPhoto, ServerSystemState
from services.time_utils import get_paris_now


def save_photo(db: Session, file: UploadFile, location: Optional[str], capture_date: Optional[datetime]) -> ServerPhoto:
    # Génération exclusive UUID v7
    new_id = str(uuid6.uuid7())
    filename = f"{new_id}.jpg"
    file_path = IMG_PATH / filename

    # Sauvegarde sur clé USB
    with open(file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

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

    # Mise à jour du state
    update_system_state(db, "last_manifest_update", now.isoformat())

    db.commit()
    db.refresh(db_photo)
    return db_photo


def get_photo_path(photo_id: str) -> Path:
    return IMG_PATH / f"{photo_id}.jpg"


def update_favorite(db: Session, photo_id: str, is_favorite: bool):
    photo = db.query(ServerPhoto).filter(ServerPhoto.id == photo_id).first()
    if photo:
        photo.is_favorite = is_favorite
        now = get_paris_now()

        # Astuce respectant votre BDD : on stocke la modif dans SystemState pour le manifest
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
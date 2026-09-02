from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session
from datetime import datetime
from typing import Optional

from models.database import get_db
from security.auth import verify_pin
from dto.manifest_dto import ManifestResponseDto
from services.manifest_service import get_manifest
from services.photo_service import update_system_state
from services.time_utils import get_paris_now

router = APIRouter(prefix="/api/manifest", tags=["Manifest"], dependencies=[Depends(verify_pin)])

@router.get("", response_model=ManifestResponseDto)
def read_manifest(since: Optional[datetime] = None, db: Session = Depends(get_db)):
    return get_manifest(db, since)

@router.post("/ack")
def acknowledge_manifest(db: Session = Depends(get_db)):
    # Le client confirme la synchro. On peut stocker cette info si besoin.
    now = get_paris_now()
    update_system_state(db, "last_client_ack", now.isoformat())
    db.commit()
    return {"status": "ok"}
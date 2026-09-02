from sqlalchemy.orm import Session
from datetime import datetime
from dto.manifest_dto import ManifestResponseDto, FavoriteChangeDto
from dto.photo_dto import PhotoMetadataDto
from models.schemas import ServerPhoto, ServerSystemState


def get_manifest(db: Session, since: datetime | None) -> ManifestResponseDto:
    created_photos = []
    deleted_ids = []
    favorites_changed = []

    # 1. Uniformisation : On s'assure que 'since' est offset-naive.
    # C# envoie généralement une date sans fuseau, mais par sécurité, on le nettoie.
    # Cela évite aussi les problèmes de requêtes avec SQLAlchemy/SQLite.
    if since is not None and since.tzinfo is not None:
        since = since.replace(tzinfo=None)

    if not since:
        # Tout récupérer
        photos = db.query(ServerPhoto).all()
        created_photos = [
            PhotoMetadataDto(
                id=p.id, filename=p.filename, is_favorite=p.is_favorite,
                capture_date=p.capture_date, location=p.location, upload_date=p.upload_date
            ) for p in photos
        ]
    else:
        # Créations
        new_photos = db.query(ServerPhoto).filter(ServerPhoto.upload_date >= since).all()
        created_photos = [
            PhotoMetadataDto(
                id=p.id, filename=p.filename, is_favorite=p.is_favorite,
                capture_date=p.capture_date, location=p.location, upload_date=p.upload_date
            ) for p in new_photos
        ]

        # Suppressions et Favoris via ServerSystemState
        states = db.query(ServerSystemState).all()
        for state in states:
            try:
                state_date = datetime.fromisoformat(state.value)

                # 2. Uniformisation : On supprime le fuseau horaire de l'état enregistré
                # afin de pouvoir le comparer avec 'since' proprement.
                if state_date.tzinfo is not None:
                    state_date = state_date.replace(tzinfo=None)

                # La comparaison passe désormais sans TypeError
                if state_date >= since:
                    if state.key.startswith("deleted_"):
                        deleted_ids.append(state.key.replace("deleted_", ""))
                    elif state.key.startswith("fav_changed_"):
                        p_id = state.key.replace("fav_changed_", "")
                        # On récupère le statut actuel
                        photo = db.query(ServerPhoto).filter(ServerPhoto.id == p_id).first()
                        if photo:
                            favorites_changed.append(FavoriteChangeDto(id=p_id, is_favorite=photo.is_favorite))
            except ValueError:
                pass  # La valeur n'est pas une date

    return ManifestResponseDto(
        created=created_photos,
        deleted=deleted_ids,
        favorites_changed=favorites_changed
    )
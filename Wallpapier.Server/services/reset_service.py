import os
from apscheduler.schedulers.background import BackgroundScheduler
from datetime import datetime, timedelta
from models.database import SessionLocal
from models.schemas import ServerPhoto, ServerSystemState
from services.photo_service import update_system_state
from configs.settings import IMG_PATH
from services.time_utils import get_paris_now, PARIS_TZ

scheduler = BackgroundScheduler(timezone=PARIS_TZ)


def perform_reset():
    """Supprime les photos non favorites et met à jour le state."""
    db = SessionLocal()
    try:
        print("[ResetService] Démarrage du nettoyage...")
        now = get_paris_now()

        # Trouver les photos non favorites
        photos_to_delete = db.query(ServerPhoto).filter(ServerPhoto.is_favorite == False).all()

        for photo in photos_to_delete:
            # 1. Supprimer le fichier original
            file_path = IMG_PATH / photo.filename
            if file_path.exists():
                os.remove(file_path)

            # 1.bis Supprimer la miniature (thumbnail)
            thumb_path = IMG_PATH / f"{photo.id}_thumb.jpg"
            if thumb_path.exists():
                os.remove(thumb_path)

            # 2. Enregistrer la suppression pour le manifest (astuce SystemState)
            update_system_state(db, f"deleted_{photo.id}", now.isoformat())

            # 3. Supprimer de la DB
            db.delete(photo)

        update_system_state(db, "last_reset_date", now.isoformat())
        db.commit()
        print(f"[ResetService] Nettoyage terminé. {len(photos_to_delete)} photos et miniatures supprimées.")
    finally:
        db.close()


def schedule_reset_job(reset_time_str: str):
    """(Re)Planifie la tâche de reset quotidienne."""
    try:
        h, m = map(int, reset_time_str.split(':'))
    except ValueError:
        h, m = 4, 0  # Par défaut 04:00

    scheduler.remove_all_jobs()
    scheduler.add_job(perform_reset, 'cron', hour=h, minute=m)
    print(f"[ResetService] Prochain reset programmé tous les jours à {h:02d}:{m:02d}.")


def init_reset_service():
    """Appelé au démarrage (startup event). Gère la reprise sur panne."""
    db = SessionLocal()
    try:
        # Récupérer l'heure configurée
        time_state = db.query(ServerSystemState).filter(ServerSystemState.key == "reset_time").first()
        reset_time_str = time_state.value if time_state else "04:00"

        # Programmer l'APScheduler
        schedule_reset_job(reset_time_str)
        scheduler.start()

        # Reprise sur panne (Check)
        last_reset_state = db.query(ServerSystemState).filter(ServerSystemState.key == "last_reset_date").first()
        now = get_paris_now()
        h, m = map(int, reset_time_str.split(':'))

        # Calcul de la dernière date théorique où le reset AURAIT DU se lancer
        theoretical_last_reset = now.replace(hour=h, minute=m, second=0, microsecond=0)
        if now < theoretical_last_reset:
            # Si l'heure actuelle est avant l'heure de reset du jour, le dernier reset théorique était hier
            theoretical_last_reset -= timedelta(days=1)

        needs_immediate_reset = False
        if not last_reset_state:
            needs_immediate_reset = True
        else:
            last_actual_reset = datetime.fromisoformat(last_reset_state.value)
            if last_actual_reset < theoretical_last_reset:
                needs_immediate_reset = True

        if needs_immediate_reset:
            print("[ResetService] Reprise sur panne : un reset a été manqué. Exécution immédiate.")
            perform_reset()

    finally:
        db.close()


def update_reset_time_in_db(new_time: str):
    db = SessionLocal()
    try:
        update_system_state(db, "reset_time", new_time)
        db.commit()
        schedule_reset_job(new_time)
    finally:
        db.close()
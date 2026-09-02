import pytz
from datetime import datetime

PARIS_TZ = pytz.timezone("Europe/Paris")

def get_paris_now() -> datetime:
    """Retourne l'heure actuelle stricte sur le fuseau de Paris."""
    return datetime.now(PARIS_TZ)
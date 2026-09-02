import uvicorn
from fastapi import FastAPI
from contextlib import asynccontextmanager

from models.database import engine, Base
from services.reset_service import init_reset_service
from controllers import manifest_controller, photo_controller, system_controller
from configs.settings import PORT

# Initialisation des tables SQLite (si elles n'existent pas)
Base.metadata.create_all(bind=engine)


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Événement de démarrage (Startup)
    print("Démarrage du serveur Wallpapier...")
    init_reset_service()  # Gère la reprise sur panne et lance l'APScheduler

    yield

    # Événement d'extinction (Shutdown)
    print("Extinction du serveur...")


app = FastAPI(
    title="Wallpapier Server",
    description="API pour la synchronisation de photos via Tailscale",
    lifespan=lifespan
)

# Enregistrement des routeurs
app.include_router(manifest_controller.router)
app.include_router(photo_controller.router)
app.include_router(system_controller.router)

if __name__ == "__main__":
    # Host à 0.0.0.0 permet l'écoute sur l'interface Tailscale
    uvicorn.run("main:app", host="0.0.0.0", port=PORT, reload=False)
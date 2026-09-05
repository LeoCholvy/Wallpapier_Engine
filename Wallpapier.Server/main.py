import uvicorn
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware # 1. Import du middleware
from contextlib import asynccontextmanager

from models.database import engine, Base
from services.reset_service import init_reset_service
from controllers import manifest_controller, photo_controller, system_controller
from configs.settings import PORT

Base.metadata.create_all(bind=engine)

@asynccontextmanager
async def lifespan(app: FastAPI):
    print("Démarrage du serveur Wallpapier...")
    init_reset_service()
    yield
    print("Extinction du serveur...")

app = FastAPI(
    title="Wallpapier Server",
    description="API pour la synchronisation de photos via Tailscale",
    lifespan=lifespan
)

# 2. Configuration stricte du CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], # Autorise les requêtes de n'importe quelle IP (localhost, iPhone sur Tailscale, etc.)
    allow_credentials=False,
    allow_methods=["*"], # Autorise GET, POST, PATCH, OPTIONS, etc.
    allow_headers=["*"], # Crucial : Autorise le passage de notre header X-PIN-Code
)

app.include_router(manifest_controller.router)
app.include_router(photo_controller.router)
app.include_router(system_controller.router)

if __name__ == "__main__":
    uvicorn.run("main:app", host="0.0.0.0", port=PORT, reload=False)
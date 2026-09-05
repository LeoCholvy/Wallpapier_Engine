import { useState, useEffect } from "react";
import type { AppSettings, UploadItem } from "../types";
import { uploadPhoto } from "../services/apiClient";
import { processImage } from "../services/imageProcessor";

export const useAppStore = () => {
    const [settings, setSettings] = useState<AppSettings>(() => {
        const saved = localStorage.getItem("wallpapier_settings");
        return saved ? JSON.parse(saved) : { serverIp: "", pinCode: "", maxResolution: "QHD" };
    });

    const [queue, setQueue] = useState<UploadItem[]>([]);
    const [globalLocation, setGlobalLocation] = useState("");

    useEffect(() => {
        localStorage.setItem("wallpapier_settings", JSON.stringify(settings));
    }, [settings]);

    // Traitement séquentiel de la file d'attente (1 par 1)
    useEffect(() => {
        const processQueue = async () => {
            const nextItem = queue.find(item => item.status === "pending");
            if (!nextItem) return;

            // 1. Début conversion
            setQueue(q => q.map(i => i.id === nextItem.id ? { ...i, status: "converting" } : i));
            try {
                // La fonction retourne maintenant l'objet ProcessedImages { main, thumb }
                const processedImages = await processImage(nextItem.file, settings.maxResolution);

                setQueue(q => q.map(i => i.id === nextItem.id ? { ...i, status: "uploading", progress: 0 } : i));

                // On envoie l'objet entier au client API
                await uploadPhoto(settings, processedImages, globalLocation, (percent) => {
                    setQueue(q => q.map(i => i.id === nextItem.id ? { ...i, progress: percent } : i));
                });

                // 3. Succès
                setQueue(q => q.map(i => i.id === nextItem.id ? { ...i, status: "done", progress: 100 } : i));

            } catch (err) {
                console.error(err);
                setQueue(q => q.map(i => i.id === nextItem.id ? { ...i, status: "error", error: "Échec de l'envoi" } : i));
            }
        };

        // Si on a un élément en attente et qu'AUCUN élément n'est actuellement en cours de traitement
        const isProcessing = queue.some(i => i.status === "converting" || i.status === "uploading");
        if (!isProcessing) {
            processQueue();
        }
    }, [queue, settings, globalLocation]);

    const addFilesToQueue = (files: FileList) => {
        const newItems = Array.from(files).map(f => ({
            id: Math.random().toString(36).substring(7),
            file: f,
            status: "pending" as const,
            progress: 0
        }));
        setQueue(q => [...q, ...newItems]);
    };

    const clearDone = () => setQueue(q => q.filter(i => i.status !== "done"));

    return { settings, setSettings, queue, addFilesToQueue, clearDone, globalLocation, setGlobalLocation };
};
import React, { useEffect, useState } from "react";
import type { AppSettings, PhotoMetadata } from "../types"; // <-- Ajout de "type"
import { fetchManifest, toggleFavorite, getBaseUrl } from "../services/apiClient";
import { Star, Loader2 } from "lucide-react";
import clsx from "clsx";

interface Props { settings: AppSettings; }

export const HistoryView: React.FC<Props> = ({ settings }) => {
    const [photos, setPhotos] = useState<PhotoMetadata[]>([]);
    const [loading, setLoading] = useState(true);
    const [showModal, setShowModal] = useState<string | null>(null);

    useEffect(() => {
        loadPhotos();
    }, [settings]);

    const loadPhotos = async () => {
        setLoading(true);
        try {
            const data = await fetchManifest(settings);
            setPhotos(data.sort((a, b) => new Date(b.upload_date).getTime() - new Date(a.upload_date).getTime()));
        } catch (e) {
            console.error(e);
        }
        setLoading(false);
    };

    const handleToggleFav = async (id: string, isFav: boolean) => {
        if (isFav) { // Si c'est déjà en favori et qu'on veut le retirer
            const dontShow = localStorage.getItem("dontShowUnfavoriteWarning");
            if (dontShow !== "true") {
                setShowModal(id);
                return;
            }
        }
        executeToggle(id, !isFav);
    };

    const executeToggle = async (id: string, newStatus: boolean) => {
        setShowModal(null);
        setPhotos(p => p.map(photo => photo.id === id ? { ...photo, is_favorite: newStatus } : photo));
        try {
            await toggleFavorite(settings, id, newStatus);
        } catch (e) {
            // Revert if error
            setPhotos(p => p.map(photo => photo.id === id ? { ...photo, is_favorite: !newStatus } : photo));
        }
    };

    if (loading) return <div className="flex justify-center items-center h-screen pb-24"><Loader2 size={32} className="animate-spin text-blue-500" /></div>;

    return (
        <div className="p-4 pb-24">
            <h1 className="text-2xl font-bold mb-6 text-gray-800">Historique</h1>

            <div className="grid grid-cols-2 gap-3">
                {photos.map(photo => (
                    <div key={photo.id} className="relative rounded-xl overflow-hidden shadow-sm aspect-square bg-gray-200">
                        {/* L'URL de l'image pointe sur la nouvelle route /thumb avec le PIN */}
                        <img
                            src={`${getBaseUrl(settings.serverIp)}/api/photos/${photo.id}/thumb?pin=${settings.pinCode}`}
                            className="w-full h-full object-cover"
                            loading="lazy"
                        />
                        <button
                            onClick={() => handleToggleFav(photo.id, photo.is_favorite)}
                            className="absolute top-2 right-2 p-2 bg-black/30 backdrop-blur-md rounded-full text-white"
                        >
                            <Star size={18} className={clsx(photo.is_favorite && "fill-yellow-400 text-yellow-400")} />
                        </button>
                    </div>
                ))}
            </div>

            {showModal && (
                <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
                    <div className="bg-white rounded-2xl p-6 w-full max-w-sm">
                        <h3 className="text-lg font-bold mb-2">Retirer des favoris ?</h3>
                        <p className="text-gray-600 text-sm mb-6">Cette photo redeviendra "normale" et sera définitivement supprimée lors du prochain nettoyage quotidien du serveur (si elle a déjà été affichée).</p>
                        <div className="flex flex-col gap-3">
                            <button onClick={() => executeToggle(showModal, false)} className="w-full bg-red-500 text-white p-3 rounded-xl font-medium">Oui, retirer</button>
                            <button
                                onClick={() => {
                                    localStorage.setItem("dontShowUnfavoriteWarning", "true");
                                    executeToggle(showModal, false);
                                }}
                                className="w-full bg-gray-100 text-gray-700 p-3 rounded-xl font-medium text-sm"
                            >
                                Oui, et ne plus me prévenir
                            </button>
                            <button onClick={() => setShowModal(null)} className="w-full p-3 font-medium text-gray-500">Annuler</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};
import React, { useRef } from "react";
import type { UploadItem } from "../types";
import { ImagePlus, Loader2, CheckCircle2, AlertCircle } from "lucide-react";

interface Props {
    queue: UploadItem[];
    addFiles: (files: FileList) => void;
    location: string;
    setLocation: (val: string) => void;
    clearDone: () => void;
}

export const UploadView: React.FC<Props> = ({ queue, addFiles, location, setLocation, clearDone }) => {
    const fileInputRef = useRef<HTMLInputElement>(null);

    const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (e.target.files && e.target.files.length > 0) {
            addFiles(e.target.files);
        }
    };

    return (
        <div className="p-4 pb-24">
            <h1 className="text-2xl font-bold mb-6 text-gray-800">Ajouter des photos</h1>

            <div className="mb-6">
                <label className="block text-sm font-medium text-gray-700 mb-1">Lieu (Optionnel, appliqué au lot)</label>
                <input
                    type="text"
                    value={location}
                    onChange={(e) => setLocation(e.target.value)}
                    placeholder="Ex: Paris, France"
                    className="w-full border rounded-lg p-3 outline-none focus:ring-2 focus:ring-blue-500"
                />
            </div>

            <button
                onClick={() => fileInputRef.current?.click()}
                className="w-full bg-blue-600 active:bg-blue-700 text-white p-4 rounded-xl flex items-center justify-center gap-3 font-semibold shadow-lg transition-transform active:scale-95"
            >
                <ImagePlus size={24} /> Sélectionner des images
            </button>
            <input
                type="file"
                multiple
                accept="image/jpeg, image/png, image/heic"
                ref={fileInputRef}
                onChange={handleFileChange}
                className="hidden"
            />

            {queue.length > 0 && (
                <div className="mt-8">
                    <div className="flex justify-between items-center mb-4">
                        <h2 className="font-semibold text-gray-700">File d'attente ({queue.length})</h2>
                        <button onClick={clearDone} className="text-sm text-blue-600">Nettoyer terminés</button>
                    </div>

                    <div className="space-y-3">
                        {queue.map((item) => (
                            <div key={item.id} className="bg-white p-3 rounded-lg shadow-sm border flex items-center gap-4">
                                <div className="w-12 h-12 bg-gray-100 rounded-md overflow-hidden flex-shrink-0">
                                    <img src={URL.createObjectURL(item.file)} className="w-full h-full object-cover" />
                                </div>
                                <div className="flex-1 min-w-0">
                                    <p className="text-sm font-medium text-gray-800 truncate">{item.file.name}</p>

                                    {item.status === "converting" && <p className="text-xs text-blue-500 flex items-center gap-1 mt-1"><Loader2 size={12} className="animate-spin" /> Compression iPhone...</p>}
                                    {item.status === "uploading" && (
                                        <div className="mt-2 w-full bg-gray-200 rounded-full h-1.5">
                                            <div className="bg-blue-600 h-1.5 rounded-full transition-all duration-300" style={{ width: `${item.progress}%` }}></div>
                                        </div>
                                    )}
                                    {item.status === "done" && <p className="text-xs text-green-600 flex items-center gap-1 mt-1"><CheckCircle2 size={12} /> Envoyé</p>}
                                    {item.status === "error" && <p className="text-xs text-red-500 flex items-center gap-1 mt-1"><AlertCircle size={12} /> {item.error}</p>}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}
        </div>
    );
};
import React, { useState } from "react";
import type { AppSettings } from "../types";
import { fetchServerStatus } from "../services/apiClient";
import { Loader2, CheckCircle2 } from "lucide-react";

interface Props { settings: AppSettings; setSettings: (s: AppSettings) => void; }

export const SettingsView: React.FC<Props> = ({ settings, setSettings }) => {
    const [testing, setTesting] = useState(false);
    const [success, setSuccess] = useState(false);

    const handleTest = async () => {
        setTesting(true); setSuccess(false);
        try {
            const status = await fetchServerStatus(settings);
            if (status.max_resolution) {
                setSettings({ ...settings, maxResolution: status.max_resolution });
                setSuccess(true);
            }
        } catch (e) {
            alert("Erreur de connexion : Vérifiez l'IP ou le PIN.");
        }
        setTesting(false);
    };

    return (
        <div className="p-4 pb-24">
            <h1 className="text-2xl font-bold mb-6 text-gray-800">Paramètres</h1>

            <div className="space-y-4">
                <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">IP du Serveur (Tailscale)</label>
                    <input
                        type="text"
                        value={settings.serverIp}
                        onChange={(e) => setSettings({ ...settings, serverIp: e.target.value })}
                        placeholder="ex: 100.64.0.1:8000"
                        className="w-full border rounded-lg p-3 outline-none focus:ring-2 focus:ring-blue-500"
                    />
                </div>

                <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">Code PIN</label>
                    <input
                        type="password"
                        inputMode="numeric"
                        value={settings.pinCode}
                        onChange={(e) => setSettings({ ...settings, pinCode: e.target.value })}
                        className="w-full border rounded-lg p-3 outline-none focus:ring-2 focus:ring-blue-500"
                    />
                </div>

                <button
                    onClick={handleTest}
                    disabled={testing}
                    className="w-full mt-4 bg-gray-800 text-white p-4 rounded-xl flex justify-center items-center gap-2 font-semibold"
                >
                    {testing ? <Loader2 className="animate-spin" /> : "Tester la connexion & Maj Résolution"}
                </button>

                {success && <p className="text-green-600 flex items-center justify-center gap-2 mt-4"><CheckCircle2 /> Connecté (Qualité max: {settings.maxResolution})</p>}
            </div>
        </div>
    );
};
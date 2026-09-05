// import React from "react";
import { Link, useLocation } from "react-router-dom";
import { Upload, Clock, Settings } from "lucide-react";
import clsx from "clsx";

export const BottomTabBar = () => {
    const loc = useLocation();

    const tabs = [
        { to: "/", icon: Upload, label: "Envoyer" },
        { to: "/history", icon: Clock, label: "Historique" },
        { to: "/settings", icon: Settings, label: "Paramètres" },
    ];

    return (
        <div className="fixed bottom-0 left-0 right-0 bg-white border-t pb-safe flex justify-around">
            {tabs.map((t) => {
                const Icon = t.icon;
                const isActive = loc.pathname === t.to;
                return (
                    <Link key={t.to} to={t.to} className={clsx("flex flex-col items-center p-3 flex-1", isActive ? "text-blue-600" : "text-gray-400")}>
                        <Icon size={24} />
                        <span className="text-[10px] mt-1 font-medium">{t.label}</span>
                    </Link>
                );
            })}
        </div>
    );
};
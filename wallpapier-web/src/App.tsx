import { HashRouter, Routes, Route } from "react-router-dom";
import { useAppStore } from "./hooks/useAppStore";
import { BottomTabBar } from "./components/BottomTabBar";
import { UploadView } from "./views/UploadView";
import { HistoryView } from "./views/HistoryView";
import { SettingsView } from "./views/SettingsView";

function App() {
  const store = useAppStore();

  if (!store.settings.serverIp || !store.settings.pinCode) {
    return <SettingsView settings={store.settings} setSettings={store.setSettings} />;
  }

  return (
      <HashRouter>
        <div className="min-h-screen bg-gray-50 text-gray-900 font-sans">
          <Routes>
            <Route path="/" element={<UploadView queue={store.queue} addFiles={store.addFilesToQueue} location={store.globalLocation} setLocation={store.setGlobalLocation} clearDone={store.clearDone} />} />
            <Route path="/history" element={<HistoryView settings={store.settings} />} />
            <Route path="/settings" element={<SettingsView settings={store.settings} setSettings={store.setSettings} />} />
          </Routes>
          <BottomTabBar />
        </div>
      </HashRouter>
  );
}

export default App;
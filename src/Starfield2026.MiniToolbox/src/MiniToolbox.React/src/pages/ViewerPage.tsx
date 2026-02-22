import { DaeViewport } from '../components/viewer/DaeViewport';
import { ClipBrowser } from '../components/viewer/ClipBrowser';
import { TransportBar } from '../components/viewer/TransportBar';
import { PropertiesPanel } from '../components/viewer/PropertiesPanel';
import { useViewerPage } from '../hooks/useViewerPage';

export function ViewerPage() {
    const vm = useViewerPage();

    return (
        <div className="flex h-full">
            {/* Left: Clip Browser */}
            <ClipBrowser
                isOpen={vm.clipPanelOpen}
                onToggle={vm.toggleClipPanel}
                clips={vm.sortedClips}
                selectedClip={vm.selectedClip}
                onSelectClip={vm.setSelectedClip}
                folderPath={vm.viewerFolder}
                onRefresh={vm.refreshClips}
            />

            {/* Center: Viewport + Transport */}
            <div className="flex-1 flex flex-col min-w-0">
                <div className="flex-1 bg-bg relative">
                    <DaeViewport
                        folderPath={vm.viewerFolder}
                        clipName={vm.selectedClip}
                        showWireframe={vm.renderSettings.showWireframe}
                        showSkeleton={vm.renderSettings.showSkeleton}
                        showGrid={vm.renderSettings.showGrid}
                        showTextures={vm.renderSettings.showTextures}
                        lightIntensity={vm.lightIntensity}
                        onModelLoaded={vm.handleModelLoaded}
                    />
                </div>
                <TransportBar disabled={!vm.selectedClip} />
            </div>

            {/* Right: Properties */}
            <PropertiesPanel
                isOpen={vm.propsPanelOpen}
                onToggle={vm.togglePropsPanel}
                selectedClip={vm.selectedClip}
                modelInfo={vm.modelInfo}
                renderSettings={vm.renderSettings}
                lightIntensity={vm.lightIntensity}
                onSetShowWireframe={vm.setShowWireframe}
                onSetShowSkeleton={vm.setShowSkeleton}
                onSetShowGrid={vm.setShowGrid}
                onSetShowTextures={vm.setShowTextures}
                onSetLightIntensity={vm.setLightIntensity}
            />
        </div>
    );
}

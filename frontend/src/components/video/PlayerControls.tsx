import React, { useState, useRef, useEffect } from 'react';
import { Play, Pause, Volume2, VolumeX, Maximize, Settings } from 'lucide-react';

interface PlayerControlsProps {
  isPlaying: boolean;
  onTogglePlay: () => void;
  progress: number;
  onSeek: (percent: number) => void;
  volume: number;
  isMuted: boolean;
  onVolumeChange: (value: number) => void;
  onToggleMute: () => void;
  onToggleFullscreen: () => void;
  durationFormatted: string;
  currentTimeFormatted: string;
  onPlaybackRateChange?: (rate: number) => void;
  onQualityChange?: (quality: string) => void;
  visible: boolean;
}

export default function PlayerControls({
  isPlaying,
  onTogglePlay,
  progress,
  onSeek,
  volume,
  isMuted,
  onVolumeChange,
  onToggleMute,
  onToggleFullscreen,
  durationFormatted,
  currentTimeFormatted,
  onPlaybackRateChange,
  onQualityChange,
  visible
}: PlayerControlsProps) {

  const [settingsOpen, setSettingsOpen] = useState(false);
  const settingsRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (settingsRef.current && !settingsRef.current.contains(e.target as Node)) {
        setSettingsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleProgressClick = (e: React.MouseEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const percent = Math.max(0, Math.min(100, (x / rect.width) * 100));
    onSeek(percent);
  };

  return (
    <div className={`absolute bottom-0 left-0 right-0 bg-gradient-to-t from-black/95 via-black/60 to-transparent pt-14 pb-4 px-2 sm:px-4 flex flex-col gap-2 transition-all duration-300 z-30 ${visible ? 'opacity-100 pointer-events-auto' : 'opacity-0 pointer-events-none'}`} dir="ltr">
      
      {/* Progress Bar */}
      <div 
        className="w-full h-1.5 cursor-pointer relative group/progress mb-2"
        onClick={handleProgressClick}
      >
        <div className="absolute inset-0 bg-[#4a3d2e] rounded-full overflow-hidden">
          {/* Fill */}
          <div 
            className="absolute top-0 bottom-0 z-10 transition-all duration-100"
            style={{ left: 0, width: `${progress}%`, backgroundColor: '#C6A87C' }}
          />
        </div>
        
        {/* Scrubber knob */}
        <div 
          className="absolute top-1/2 -translate-y-1/2 w-3 h-3 bg-white rounded-full transition-opacity opacity-0 group-hover/progress:opacity-100 z-20"
          style={{ left: `calc(${progress}% - 6px)` }}
        />
      </div>

      {/* Controls Row */}
      <div className="flex items-center justify-between text-white">
        
        {/* Left section */}
        <div className="flex items-center gap-4">
          <button 
            onClick={onTogglePlay}
            className="transition-colors focus:outline-none hover:text-[#C6A87C]"
          >
            {isPlaying ? <Pause className="w-6 h-6" fill="currentColor" /> : <Play className="w-6 h-6" fill="currentColor" />}
          </button>

          <div className="flex items-center gap-2 group/volume relative">
            <button 
              onClick={onToggleMute}
              className="transition-colors focus:outline-none hover:text-[#C6A87C]"
            >
              {isMuted || volume === 0 ? <VolumeX className="w-5 h-5" /> : <Volume2 className="w-5 h-5" />}
            </button>
            <input 
              type="range" 
              min="0" 
              max="100" 
              value={isMuted ? 0 : volume}
              onChange={(e) => onVolumeChange(parseInt(e.target.value))}
              className="w-0 opacity-0 group-hover/volume:w-20 group-hover/volume:opacity-100 transition-all duration-300 cursor-pointer"
              style={{ accentColor: '#C6A87C' }}
            />
          </div>

          <div className="text-sm font-medium tracking-wide font-mono">
            {currentTimeFormatted} / {durationFormatted}
          </div>
        </div>

        {/* Right section */}
        <div className="flex items-center gap-4 relative">
          
          {/* Settings Menu */}
          <div className="relative" ref={settingsRef}>
            <button 
              onClick={() => setSettingsOpen(!settingsOpen)}
              className={`transition-colors focus:outline-none ${settingsOpen ? 'text-[#C6A87C]' : 'hover:text-[#C6A87C]'}`}
            >
              <Settings className={`w-5 h-5 transition-transform ${settingsOpen ? 'rotate-90' : ''}`} />
            </button>
            
            {settingsOpen && (
              <div className="absolute bottom-full right-0 mb-4 bg-zinc-900 border border-[#C6A87C]/30 rounded-lg p-3 flex gap-6 shadow-xl z-50 origin-bottom-right w-max text-right">
                
                {/* Speed Menu */}
                <div className="flex flex-col gap-1 w-28 text-right [direction:rtl]">
                  <div className="text-xs text-gray-400 px-2 py-1 mb-1 border-b border-white/10 tracking-wider font-semibold">سرعة التشغيل</div>
                  {[0.5, 0.75, 1, 1.25, 1.5, 1.75, 2].map(speed => (
                    <button 
                      key={`speed-${speed}`}
                      className="text-right px-3 py-1.5 text-sm text-white hover:bg-[#C6A87C]/20 hover:text-[#C6A87C] rounded transition-colors w-full"
                      onClick={() => {
                        if (onPlaybackRateChange) onPlaybackRateChange(speed);
                        setSettingsOpen(false);
                      }}
                    >
                      {speed === 1 ? 'عادي' : `${speed}x`}
                    </button>
                  ))}
                </div>

                {/* Quality Menu (if supported) */}
                <div className="flex flex-col gap-1 w-28 text-right [direction:rtl]">
                  <div className="text-xs text-gray-400 px-2 py-1 mb-1 border-b border-white/10 tracking-wider font-semibold">الجودة</div>
                  {['highres', 'hd1080', 'hd720', 'large', 'medium', 'small', 'auto'].map(q => (
                    <button 
                      key={`qual-${q}`}
                      className="text-right px-3 py-1.5 text-sm text-white hover:bg-[#C6A87C]/20 hover:text-[#C6A87C] rounded transition-colors w-full uppercase"
                      onClick={() => {
                        if (onQualityChange) onQualityChange(q);
                        setSettingsOpen(false);
                      }}
                    >
                      {q === 'auto' ? 'تلقائي' : q.replace('hd', '').replace('large', '480p').replace('medium', '360p').replace('small', '240p').replace('highres', '1440p+')}
                    </button>
                  ))}
                </div>

              </div>
            )}
          </div>
          
          <button 
            onClick={onToggleFullscreen}
            className="transition-colors focus:outline-none hover:text-[#C6A87C]"
          >
            <Maximize className="w-5 h-5" />
          </button>
        </div>
      </div>
    </div>
  );
}

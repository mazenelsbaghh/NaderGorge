# Data Model: Custom Animated Video Player Controls

**Branch**: `033-custom-video-player`
**Phase**: 1 — Design & Contracts (Updated)
**Date**: 2026-03-31

> Pure UI component feature. No database tables or backend entities. This file documents the **component state model** and **props contracts**.

---

## `CustomSlider` Sub-component

Internal to `PlayerControls.tsx` (not exported).

### Props

| Prop | Type | Description |
|------|------|-------------|
| `value` | `number` | Fill level as `0–100` percent |
| `onChange` | `(value: number) => void` | Called with clamped `0–100` position |
| `className` | `string?` | Optional Tailwind class override |

### Internal State

| State | Type | Default | Description |
|-------|------|---------|-------------|
| `isDragging` | `boolean` | `false` | Whether a drag operation is in progress |

### Behavior Rules

- **Click**: `onClick` on the div computes `(x / width) * 100`, clamped, calls `onChange`.
- **Drag**: `onMouseDown` sets `isDragging = true`. Global `document.mousemove` recalculates position and calls `onChange`. Global `document.mouseup` sets `isDragging = false`.
- **Clamping**: All computed percentages are clamped to `[0, 100]` before calling `onChange`.
- **Fill animation**: `motion.div` with `initial={{ width: 0 }}`, `animate={{ width: \`${value}%\` }}`, `transition={{ type: "spring", stiffness: 300, damping: 30 }}`.
- **Out-of-bounds drag**: Because listeners are on `document`, dragging outside the div continues tracking until `mouseup`.

---

## `PlayerControls` Component

### Props (Unchanged Interface — FR-015)

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `isPlaying` | `boolean` | ✅ | Current playback state |
| `onTogglePlay` | `() => void` | ✅ | Toggle play/pause |
| `progress` | `number` | ✅ | Playback progress `0–100` |
| `onSeek` | `(percent: number) => void` | ✅ | Seek to percent position |
| `volume` | `number` | ✅ | Volume `0–100` |
| `isMuted` | `boolean` | ✅ | Muted state |
| `onVolumeChange` | `(value: number) => void` | ✅ | Set volume `0–100` |
| `onToggleMute` | `() => void` | ✅ | Toggle mute |
| `onToggleFullscreen` | `() => void` | ✅ | Enter/exit fullscreen |
| `durationFormatted` | `string` | ✅ | Total duration as `M:SS` |
| `currentTimeFormatted` | `string` | ✅ | Current time as `M:SS` |
| `onPlaybackRateChange` | `(rate: number) => void` | ❌ | Optional speed callback |
| `onQualityChange` | `(quality: string) => void` | ❌ | Optional quality callback |
| `visible` | `boolean` | ✅ | AnimatePresence gate |

### Internal State

| State | Type | Default | Description |
|-------|------|---------|-------------|
| `playbackSpeed` | `number` | `1` | Active speed chip highlight |
| `settingsOpen` | `boolean` | `false` | Quality popover open |

---

## `SecureVideoPlayer` Pause Overlay Change

Only the **anti-suggestions blur section** (lines 408–416 in original) changes:

### Before (bottom-only 40% blur)

```tsx
// h-[40%] covers only the bottom
<div className="absolute bottom-0 left-0 right-0 h-[40%] bg-black/60 backdrop-blur-[12px] ...">
  <div>... play icon ...</div>
  <span>تم الإيقاف</span>
</div>
```

### After (full-screen blur with centred play button)

```tsx
// inset-0 covers entire video area
<motion.div className="absolute inset-0 bg-black/50 backdrop-blur-[14px] z-10 flex flex-col items-center justify-center animate-in fade-in duration-300">
  <motion.button onClick={togglePlay} whileHover={{ scale: 1.1 }} whileTap={{ scale: 0.95 }}>
    <div className="w-20 h-20 rounded-full bg-pharaoh-gold/20 border border-pharaoh-gold/50 backdrop-blur-md flex items-center justify-center shadow-lg">
      <Play className="w-10 h-10 text-pharaoh-gold ml-1" />
    </div>
  </motion.button>
  <span className="text-pharaoh-sand font-bold text-sm tracking-widest mt-4 bg-black/50 px-4 py-1 rounded-full">تم الإيقاف</span>
</motion.div>
```

---

## Animation State Transitions — Controls

```
Paused / idle (visible = true, always shown when not playing)
   ↓ user starts playing → controls shown then auto-hide after 3s
Playing (visible = false after 3s)
   ↓ mouse enters video area
Controls mounting (AnimatePresence):
   → y: 20→0, opacity: 0→1, filter: blur(10px)→blur(0px)
   → transition: { duration: 0.6, ease: "circInOut", type: "spring" }
Controls visible (visible = true)
   ↓ mouse leaves
Controls unmounting (AnimatePresence exit):
   → y: 0→20, opacity: 1→0, filter: blur(0px)→blur(10px)
```

## Animation State Transitions — Pause Overlay

```
Playing → isPlaying = false, currentTime > 0
   → Pause overlay mounts with fade-in 300ms
Paused → user clicks play button
   → Pause overlay unmounts
   → Controls pill appears (showControls = true while paused)
```

---

## Quality Options

| Value | Display |
|-------|---------|
| `highres` | 1440p+ |
| `hd1080` | 1080p |
| `hd720` | 720p |
| `large` | 480p |
| `medium` | 360p |
| `small` | 240p |
| `auto` | تلقائي |

## Playback Speed Options

| Value | Active class |
|-------|-------------|
| `0.5` | `bg-[#111111d1]` |
| `1` | `bg-[#111111d1]` |
| `1.5` | `bg-[#111111d1]` |
| `2` | `bg-[#111111d1]` |

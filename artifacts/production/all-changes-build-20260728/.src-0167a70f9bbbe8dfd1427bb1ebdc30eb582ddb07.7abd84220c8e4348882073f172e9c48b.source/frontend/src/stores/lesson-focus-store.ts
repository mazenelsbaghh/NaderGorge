import { create } from 'zustand';

interface LessonFocusState {
  isFocusMode: boolean;
  setFocusMode: (value: boolean) => void;
  toggleFocusMode: () => void;
}

export const useLessonFocusStore = create<LessonFocusState>((set) => ({
  isFocusMode: false,
  setFocusMode: (value) => set({ isFocusMode: value }),
  toggleFocusMode: () => set((state) => ({ isFocusMode: !state.isFocusMode })),
}));

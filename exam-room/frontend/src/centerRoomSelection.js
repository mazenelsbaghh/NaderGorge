// A room created in the native center workspace must become visible without
// remounting this page. Keep an active/draft room or an open grading form intact.
export function centerRoomSelection(rooms, selectedId, protectedView = false) {
  if (protectedView) return null;
  const current = rooms.find(room => room.id === selectedId);
  const active = rooms.find(room => ['waiting', 'running'].includes(room.state));
  if (!current) return active?.id || rooms[0]?.id || null;
  if (current.state === 'closed' && active && active.createdAt > current.createdAt) return active.id;
  return null;
}

export type BunnyVideoReference = {
  libraryId: string;
  videoGuid: string;
};

export type BunnyVideoInputReference = {
  libraryId?: string;
  videoGuid: string;
};

const BUNNY_LIBRARY_ID_PATTERN = /^[1-9]\d{0,18}$/;
const BUNNY_VIDEO_GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const BUNNY_PLAYER_PATH_PATTERN = /^\/(?:play|embed)\/([1-9]\d{0,18})\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\/?$/i;

export function isBunnyLibraryId(libraryId: string) {
  return BUNNY_LIBRARY_ID_PATTERN.test(libraryId);
}

export function isBunnyVideoGuid(videoGuid: string) {
  return BUNNY_VIDEO_GUID_PATTERN.test(videoGuid);
}

export function parseScopedBunnyVideoReference(value: string): BunnyVideoReference | undefined {
  const separatorIndex = value.indexOf('/');
  if (separatorIndex <= 0 || separatorIndex !== value.lastIndexOf('/')) return undefined;

  const libraryId = value.slice(0, separatorIndex);
  const videoGuid = value.slice(separatorIndex + 1);
  return isBunnyLibraryId(libraryId) && isBunnyVideoGuid(videoGuid)
    ? { libraryId, videoGuid }
    : undefined;
}

export function parseBunnyPlayerPath(pathname: string): BunnyVideoReference | undefined {
  const match = BUNNY_PLAYER_PATH_PATTERN.exec(pathname);
  return match ? { libraryId: match[1], videoGuid: match[2] } : undefined;
}

/**
 * Parses the two references accepted by the content form:
 * a bare video GUID (which still needs an explicit library selection), or a
 * complete Bunny player URL whose library can be selected automatically.
 */
export function parseBunnyVideoReference(value: string): BunnyVideoInputReference | undefined {
  const trimmed = value.trim();
  if (isBunnyVideoGuid(trimmed)) {
    return { videoGuid: trimmed };
  }

  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    return undefined;
  }

  if (
    url.protocol !== 'https:' ||
    url.username ||
    url.password ||
    (url.hostname !== 'player.mediadelivery.net' &&
      url.hostname !== 'iframe.mediadelivery.net')
  ) {
    return undefined;
  }

  return parseBunnyPlayerPath(url.pathname);
}

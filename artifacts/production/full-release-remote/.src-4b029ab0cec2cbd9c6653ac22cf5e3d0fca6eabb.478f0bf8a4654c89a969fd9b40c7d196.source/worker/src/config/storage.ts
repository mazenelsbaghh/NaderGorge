import fs from 'fs';
import path from 'path';

const developmentPublicRoot = path.resolve(
  process.cwd(),
  '../backend/src/NaderGorge.API/wwwroot',
);

export const sharedStorageRoot = path.resolve(
  process.env.SHARED_STORAGE_ROOT || path.dirname(developmentPublicRoot),
);

export const sharedPublicRoot = path.resolve(
  process.env.SHARED_PUBLIC_ROOT || (
    process.env.SHARED_STORAGE_ROOT
      ? path.join(sharedStorageRoot, 'public')
      : developmentPublicRoot
  ),
);

export const sharedMindmapsRoot = path.resolve(
  process.env.MINDMAP_STORAGE_PATH || path.join(sharedPublicRoot, 'mindmaps'),
);

export const sharedSubtitlesRoot = path.resolve(
  process.env.SUBTITLE_STORAGE_PATH || path.join(sharedPublicRoot, 'subtitles'),
);

export function resolveWithin(root: string, relativePath: string) {
  if (!relativePath || path.isAbsolute(relativePath)) {
    throw new Error('A non-empty relative storage path is required.');
  }

  const resolvedRoot = path.resolve(root);
  const resolvedPath = path.resolve(resolvedRoot, relativePath);
  if (resolvedPath !== resolvedRoot && !resolvedPath.startsWith(`${resolvedRoot}${path.sep}`)) {
    throw new Error('The storage path escapes its configured root.');
  }
  return resolvedPath;
}

export function atomicWriteFileSync(
  root: string,
  relativePath: string,
  content: string | NodeJS.ArrayBufferView,
  encoding?: BufferEncoding,
) {
  const destination = resolveWithin(root, relativePath);
  const directory = path.dirname(destination);
  fs.mkdirSync(directory, { recursive: true });
  const temporary = path.join(
    directory,
    `.${path.basename(destination)}.${process.pid}.${Date.now()}.tmp`,
  );

  try {
    const fileDescriptor = fs.openSync(temporary, 'wx', 0o640);
    try {
      if (typeof content === 'string') {
        fs.writeFileSync(fileDescriptor, content, { encoding: encoding || 'utf8' });
      } else {
        fs.writeFileSync(fileDescriptor, content);
      }
      fs.fsyncSync(fileDescriptor);
    } finally {
      fs.closeSync(fileDescriptor);
    }
    fs.renameSync(temporary, destination);
    const directoryDescriptor = fs.openSync(directory, 'r');
    try {
      fs.fsyncSync(directoryDescriptor);
    } finally {
      fs.closeSync(directoryDescriptor);
    }
    return destination;
  } catch (error) {
    fs.rmSync(temporary, { force: true });
    throw error;
  }
}

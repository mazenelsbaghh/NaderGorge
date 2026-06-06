import { execFile } from 'child_process';
import util from 'util';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import ytDlp from 'youtube-dl-exec';

const execFileAsync = util.promisify(execFile);
const ytDlpPath = (ytDlp as any).constants.YOUTUBE_DL_PATH;

// Resolve the worker root directory reliably from the compiled file location.
// dist/utils/ → ../../ = worker root
// src/utils/  → ../../ = worker root
const __filename = fileURLToPath(import.meta.url);
const __dirname  = path.dirname(__filename);
const workerRoot = path.resolve(__dirname, '../../');
const URL_PATTERN = /\bhttps?:\/\/[^\s]+/gi;

function sanitizeProcessOutput(value: unknown): string {
    const text = String(value || '').replace(URL_PATTERN, '[redacted-url]');
    return text.length > 500 ? `${text.substring(0, 500)}...` : text;
}

/**
 * Downloads a remote video and extracts a compressed MP3 audio track
 * for speech recognition by the Gemini API.
 *
 * Strategy:
 *   1. Snapshot .tmp contents before running yt-dlp
 *   2. Run yt-dlp with the canonical output template
 *   3. Find ANY new file that appeared in .tmp (yt-dlp may name it from URL, not template)
 *   4. If the new file is not .mp3, convert it to .mp3 via ffmpeg
 *   5. Surface yt-dlp errors loudly (no --no-warnings)
 */
export async function extractAudioFromVideo(sourceUrl: string, outputFileName: string): Promise<string> {
    const tempDir = path.join(workerRoot, '.tmp');
    if (!fs.existsSync(tempDir)) {
        fs.mkdirSync(tempDir, { recursive: true });
    }

    // The canonical mp3 path we always want to end up with
    const expectedMp3 = path.join(tempDir, `${outputFileName}.mp3`);

    // Short-circuit: if a previous attempt already produced the file, reuse it
    if (fs.existsSync(expectedMp3)) {
        console.log(`[Youtube-DL] Reusing existing audio: ${expectedMp3}`);
        return expectedMp3;
    }

    console.log(`[Youtube-DL] Starting extraction for job ${outputFileName}`);
    console.log(`[Youtube-DL] Source received (${sourceUrl.length} chars).`);
    console.log(`[Youtube-DL] Worker root: ${workerRoot}`);
    console.log(`[Youtube-DL] Temp dir:    ${tempDir}`);

    // Normalise raw YouTube IDs → full URL
    let url = sourceUrl;
    if (url.length === 11 && !url.includes('http')) {
        url = `https://www.youtube.com/watch?v=${url}`;
    }

    // ── Snapshot BEFORE ──────────────────────────────────────────────────────
    const filesBefore = new Set(fs.readdirSync(tempDir));

    // ── Run yt-dlp ──────────────────────────────────────────────────────────
    // Do NOT pass .mp3 extension in the -o template: yt-dlp appends the right
    // extension itself. Passing '.mp3' causes double-extension bugs (uuid.mp3.mp3).
    // Also: remove --no-warnings so errors surface in stderr.
    const outputTemplate = path.join(tempDir, outputFileName);
    const args = [
        url,
        '--extract-audio',
        '--audio-format',  'mp3',
        '--audio-quality', '5',
        '-o',              outputTemplate,
        '--no-check-certificates',
        '--no-playlist',
        '--prefer-ffmpeg',
        '--newline',
        // yt-dlp 2026+ requires a JS runtime to process YouTube's player API.
        // Pointing it to the system Node binary fixes the "no JS runtime" warning
        // that causes all YouTube formats to be invisible.
        '--js-runtimes', `node:${process.execPath}`,
    ];

    console.log(`[Youtube-DL] Running yt-dlp...`);
    try {
        const { stdout, stderr } = await execFileAsync(ytDlpPath, args);
        if (stdout) console.log(`[Youtube-DL] stdout:\n${sanitizeProcessOutput(stdout)}`);
        if (stderr) console.warn(`[Youtube-DL] stderr:\n${sanitizeProcessOutput(stderr)}`);
    } catch (err: any) {
        const errorMsg = err.stderr || err.stdout || err.message || String(err);
        const safeErrorMsg = sanitizeProcessOutput(errorMsg);
        console.error(`[Youtube-DL] yt-dlp exited with error:\n${safeErrorMsg}`);
        throw new Error(`yt-dlp failed to download. Detail: ${safeErrorMsg}`);
    }

    // ── Check expected path first ─────────────────────────────────────────────
    if (fs.existsSync(expectedMp3)) {
        console.log(`[Youtube-DL] ✅ Audio ready at expected path: ${expectedMp3}`);
        return expectedMp3;
    }

    // ── Snapshot AFTER and find new files ────────────────────────────────────
    const filesAfter  = fs.readdirSync(tempDir);
    const newFiles    = filesAfter.filter(f => !filesBefore.has(f));
    console.log(`[Youtube-DL] New files in .tmp after yt-dlp:`, newFiles);

    if (newFiles.length === 0) {
        // yt-dlp exited 0 but produced nothing — likely a format/URL issue
        throw new Error(
            `yt-dlp ran successfully but created no output file. ` +
            `URL may be unsupported or private.`
        );
    }

    // ── Find the best candidate (prefer .mp3, else first new file) ───────────
    let candidate: string | undefined =
                   newFiles.find(f => f.endsWith('.mp3'))
                ?? newFiles.find(f => f.startsWith(outputFileName))
                ?? newFiles[0];

    if (!candidate) {
        throw new Error(
            `yt-dlp ran but produced no usable file. New files seen: ${newFiles.join(', ')}`
        );
    }

    const candidatePath = path.join(tempDir, candidate);

    if (candidate.endsWith('.mp3')) {
        // It's already an mp3 — just rename to the canonical name if needed
        if (candidatePath !== expectedMp3) {
            fs.renameSync(candidatePath, expectedMp3);
            console.log(`[Youtube-DL] Renamed "${candidate}" → "${outputFileName}.mp3"`);
        }
        return expectedMp3;
    }

    // ── Convert non-mp3 to mp3 via ffmpeg ────────────────────────────────────
    console.log(`[Youtube-DL] File is not .mp3 ("${candidate}") — converting with ffmpeg...`);
    try {
        await execFileAsync('ffmpeg', [
            '-i',  candidatePath,
            '-vn',                   // strip video
            '-ar', '16000',          // 16kHz — optimal for Gemini speech recognition
            '-ac', '1',              // mono
            '-b:a', '48k',           // low bitrate — speech only
            '-y',                    // overwrite
            expectedMp3,
        ]);
        // Cleanup the intermediate file
        try { fs.unlinkSync(candidatePath); } catch { /* ignore */ }
        console.log(`[Youtube-DL] ✅ Converted to mp3: ${expectedMp3}`);
        return expectedMp3;
    } catch (ffmpegErr: any) {
        const msg = ffmpegErr.stderr || ffmpegErr.message || String(ffmpegErr);
        throw new Error(`ffmpeg conversion failed: ${msg}`);
    }
}

import { NextResponse, NextRequest } from 'next/server';
import crypto from 'crypto';

export async function GET(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const encryptedToken = searchParams.get('t');
  const base64Key = searchParams.get('k');

  if (!encryptedToken || !base64Key) {
    return new NextResponse('Missing parameters', { status: 400 });
  }

  try {
    const keyBytes = Buffer.from(base64Key, 'base64');
    const tokenBytes = Buffer.from(encryptedToken, 'base64');

    const IV_SIZE = 12;
    const TAG_SIZE = 16;

    if (tokenBytes.length < IV_SIZE + TAG_SIZE) {
      return new NextResponse('Invalid token', { status: 400 });
    }

    const iv = tokenBytes.subarray(0, IV_SIZE);
    const ciphertext = tokenBytes.subarray(IV_SIZE, tokenBytes.length - TAG_SIZE);
    const authTag = tokenBytes.subarray(tokenBytes.length - TAG_SIZE);

    const decipher = crypto.createDecipheriv('aes-256-gcm', keyBytes, iv);
    decipher.setAuthTag(authTag);

    let decrypted = decipher.update(ciphertext, undefined, 'utf8');
    decrypted += decipher.final('utf8');

    const parsed = JSON.parse(decrypted) as { Provider: string; VideoId: string };
    const videoUrl = parsed.VideoId; // The direct bot url provided by Admin

    // If it's a telegram link, fetch its headers without following max redirects, 
    // to instantly get the actual CDN stream location!
    if (parsed.Provider?.toLowerCase() === 'telegram') {
      try {
        let finalVideoUrl = videoUrl;

        // If the URL is a t.me post link, scrape the embed page to find the actual .mp4 CDN source
        if (videoUrl.includes('t.me/')) {
          const embedUrl = new URL(videoUrl);
          embedUrl.searchParams.set('embed', '1');
          
          const htmlRes = await fetch(embedUrl.toString());
          if (htmlRes.ok) {
            const html = await htmlRes.text();
            const cheerio = await import('cheerio');
            const $ = cheerio.load(html);
            const videoSrc = $('video').attr('src');
            
            if (videoSrc) {
              finalVideoUrl = videoSrc;
            } else {
               console.error('[stream-proxy] Telegram web embed says "Media is too big" or video not found.');
               return new NextResponse('Telegram web stream not available for this size. Please provide a direct bot .mp4 link instead.', { status: 400 });
            }
          }
        }

        const fetchRes = await fetch(finalVideoUrl, {
          method: 'HEAD', // HEAD is faster if we only need headers
          redirect: 'manual', 
        });

        console.log('[stream-proxy] URL:', finalVideoUrl, 'Status:', fetchRes.status);

        if ([301, 302, 303, 307, 308].includes(fetchRes.status)) {
          const finalLocation = fetchRes.headers.get('location');
          console.log('[stream-proxy] Redirected Location:', finalLocation);
          if (finalLocation) {
            return NextResponse.redirect(finalLocation, { status: 302 });
          }
        }
        
        console.log('[stream-proxy] No redirect location headers, redirecting to original bot link.');
        return NextResponse.redirect(finalVideoUrl, { status: 302 });
      } catch (err) {
        console.error('[stream-proxy] Error fetching redirect', err);
        return NextResponse.redirect(videoUrl, { status: 302 });
      }
    }
    
    return new NextResponse('Unsupported streaming provider', { status: 400 });

  } catch (error) {
    console.error('[stream-proxy] Invalid token or decryption failed:', error);
    return new NextResponse('Invalid or expired stream session', { status: 403 });
  }
}

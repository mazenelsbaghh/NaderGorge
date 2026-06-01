# Quickstart: Generating VK Embeds

1. Upload your video lesson to **vk.com / VK Video** (You can create a private community to host these).
2. Once the video is processed, open the video on VK.
3. Click the **Share** (مشاركة) button.
4. Go to the **Export** (تضمين) tab.
5. You will see an HTML iframe code. Look for the `src=""` URL inside that iframe.
   - Example: `https://vk.com/video_ext.php?oid=1234567&id=89101112&hash=abcdef123456&hd=2`
6. Copy that exact URL into the Admin Dashboard when creating a `LessonVideo`, and set the provider to `vk`.

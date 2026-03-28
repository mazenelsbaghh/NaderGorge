import CircularGallery from '@/components/ui/circular-gallery';

const GALLERY_ITEMS = Array.from({ length: 6 }).map(() => ({
  image: '/images/gallery/gallery-item-1.jpg',
  text: 'معاذ احمد\nالاول علي الجمهوريه',
}));

export function CircularGallerySection() {
  return (
    <section className="landing-content-visibility-gallery relative w-full overflow-hidden bg-background py-0 sm:py-0">
      <div className="mx-auto flex flex-col items-center justify-center">
        {/*
          The Gallery is full width, we give it a fixed height or view height.
          We use user's explicit requested props.
        */}
        <div style={{ height: '700px', position: 'relative', width: '100%' }}>
          <CircularGallery
            items={GALLERY_ITEMS}
            bend={2}
            textColor="#e2c48e" // Luxury beige/gold from brand colors
            font="800 52px Cairo, sans-serif" // Explicit font family without CSS variables, bigger weight and size
            borderRadius={0.09}
            scrollSpeed={2.6}
            scrollEase={0.09}
            autoPlay={true}
            autoPlaySpeed={0.05} // Made extremely slow as requested
          />
        </div>
      </div>
    </section>
  );
}

"use client"

import { useCallback, useEffect, useRef } from "react"

import { cn } from "@/lib/utils"

const morphTime = 1.5
const cooldownTime = 0.5

const useMorphingText = (texts: string[]) => {
  const text1Ref = useRef<HTMLSpanElement>(null)
  const text2Ref = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    let animationFrameId: number

    const animate = () => {
      animationFrameId = requestAnimationFrame(animate)

      const current1 = text1Ref.current
      const current2 = text2Ref.current
      if (!current1 || !current2) return

      const currentTime = performance.now() / 1000
      const totalCycleTime = morphTime + cooldownTime

      // We align the cycle offset so it immediately starts in cooldown for the first text
      const cycleElapsed = currentTime % totalCycleTime
      const absoluteCycle = Math.floor(currentTime / totalCycleTime)
      
      const index1 = absoluteCycle % texts.length
      const index2 = (absoluteCycle + 1) % texts.length

      current1.textContent = texts[index1]
      current2.textContent = texts[index2]

      if (cycleElapsed < cooldownTime) {
        // Cooldown period
        current1.style.filter = "none"
        current1.style.opacity = "100%"
        current2.style.filter = "none"
        current2.style.opacity = "0%"
      } else {
        // Morph period
        const fraction = (cycleElapsed - cooldownTime) / morphTime
        
        current2.style.filter = `blur(${Math.min(8 / fraction - 8, 100)}px)`
        current2.style.opacity = `${Math.pow(fraction, 0.4) * 100}%`

        const invertedFraction = 1 - fraction
        current1.style.filter = `blur(${Math.min(8 / invertedFraction - 8, 100)}px)`
        current1.style.opacity = `${Math.pow(invertedFraction, 0.4) * 100}%`
      }
    }

    animate()
    return () => {
      cancelAnimationFrame(animationFrameId)
    }
  }, [texts])

  return { text1Ref, text2Ref }
}

export interface MorphingTextProps {
  className?: string
  texts: string[]
}

const Texts: React.FC<Pick<MorphingTextProps, "texts">> = ({ texts }) => {
  const { text1Ref, text2Ref } = useMorphingText(texts)
  return (
    <>
      <span
        className="absolute inset-x-0 top-0 m-auto inline-block w-full"
        ref={text1Ref}
      />
      <span
        className="absolute inset-x-0 top-0 m-auto inline-block w-full"
        ref={text2Ref}
      />
    </>
  )
}

const SvgFilters: React.FC = () => (
  <svg
    id="filters"
    className="fixed h-0 w-0"
    preserveAspectRatio="xMidYMid slice"
  >
    <defs>
      <filter id="threshold">
        <feColorMatrix
          in="SourceGraphic"
          type="matrix"
          values="1 0 0 0 0 0 1 0 0 0 0 0 1 0 0 0 0 0 255 -140"
        />
      </filter>
    </defs>
  </svg>
)

export const MorphingText: React.FC<MorphingTextProps> = ({
  texts,
  className,
}) => (
  <div
    className={cn(
      "relative h-16 w-full font-sans text-[40pt] leading-[1.2] font-black filter-[url(#threshold)_blur(0.6px)] md:h-24 lg:text-[6.2rem]",
      className
    )}
  >
    <Texts texts={texts} />
    <SvgFilters />
  </div>
)

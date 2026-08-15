export const motionTokens = {
  micro: { duration: 0.14, ease: [0.2, 0.8, 0.2, 1] as const },
  enter: { duration: 0.24, ease: [0.22, 1, 0.36, 1] as const },
  exit: { duration: 0.18, ease: [0.4, 0, 1, 1] as const },
  responsiveSpring: {
    type: "spring" as const,
    stiffness: 520,
    damping: 38,
    mass: 0.72,
  },
  softSpring: {
    type: "spring" as const,
    stiffness: 310,
    damping: 34,
    mass: 0.92,
  },
};

import { AnimatePresence, motion } from "motion/react";
import type { ReactNode } from "react";
import { motionTokens } from "./motion";

export function PageHeader({
  title,
  subtitle,
  compact,
  action,
}: {
  title: string;
  subtitle?: string;
  compact: boolean;
  action?: ReactNode;
}) {
  return (
    <>
      <div className={`compact-header ${compact ? "is-visible" : ""}`}>
        <div className="compact-header-inner">
          <motion.strong
            animate={{ opacity: compact ? 1 : 0, y: compact ? 0 : 5 }}
            transition={motionTokens.micro}
          >
            {title}
          </motion.strong>
          {compact ? action : null}
        </div>
      </div>
      <header className="large-title-block">
        <AnimatePresence mode="popLayout" initial={false}>
          <motion.div
            key={title}
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -5 }}
            transition={motionTokens.enter}
          >
            <h1>{title}</h1>
            {subtitle ? <p>{subtitle}</p> : null}
          </motion.div>
        </AnimatePresence>
        <div className="large-title-action">{compact ? null : action}</div>
      </header>
    </>
  );
}

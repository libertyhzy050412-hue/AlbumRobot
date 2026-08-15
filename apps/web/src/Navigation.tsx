import { motion } from "motion/react";
import type { TabKey } from "./domain";
import { Icon, type IconName } from "./icons";
import { motionTokens } from "./motion";

const items: Array<{ key: TabKey; label: string; icon: IconName }> = [
  { key: "albums", label: "专辑", icon: "albums" },
  { key: "feed", label: "动态", icon: "feed" },
  { key: "stats", label: "统计", icon: "stats" },
];

export function Navigation({
  activeTab,
  onSelect,
}: {
  activeTab: TabKey;
  onSelect: (tab: TabKey) => void;
}) {
  return (
    <nav className="primary-navigation" aria-label="主导航">
      <div className="navigation-brand" aria-hidden="true">
        <span />
      </div>
      <div className="navigation-items">
        {items.map((item) => {
          const active = item.key === activeTab;
          return (
            <button
              key={item.key}
              type="button"
              className={active ? "is-active" : ""}
              aria-current={active ? "page" : undefined}
              onClick={() => onSelect(item.key)}
            >
              {active ? (
                <motion.span
                  className="navigation-selection"
                  layoutId="navigation-selection"
                  transition={motionTokens.responsiveSpring}
                />
              ) : null}
              <Icon name={item.icon} />
              <span>{item.label}</span>
            </button>
          );
        })}
      </div>
    </nav>
  );
}

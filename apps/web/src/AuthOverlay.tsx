import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { useEffect, useState, type FormEvent } from "react";
import { motionTokens } from "./motion";

export type AuthState = "checking" | "required" | "authenticated";

export function AuthOverlay({
  authState,
  initialError,
  onAuthenticated,
}: {
  authState: AuthState;
  initialError: string;
  onAuthenticated: () => void;
}) {
  const [password, setPassword] = useState("");
  const [error, setError] = useState(initialError);
  const [submitting, setSubmitting] = useState(false);
  const reducedMotion = useReducedMotion();

  useEffect(() => {
    if (initialError) setError(initialError);
  }, [initialError]);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!password || submitting) return;
    setSubmitting(true);
    setError("");
    try {
      const response = await fetch("/api/auth/session", {
        method: "POST",
        credentials: "same-origin",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ password }),
      });
      if (!response.ok) {
        setError(
          response.status === 401
            ? "密码不正确，请重新输入。"
            : response.status === 429
              ? "尝试次数较多，请稍后再试。"
              : "暂时无法验证，请稍后重试。",
        );
        return;
      }
      setPassword("");
      onAuthenticated();
    } catch {
      setError("无法连接 AlbumRobot，请检查网络后重试。");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AnimatePresence>
      {authState !== "authenticated" ? (
        <motion.div
          className="auth-scrim"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={motionTokens.enter}
        >
          <motion.section
            className="auth-panel"
            aria-labelledby="auth-title"
            initial={
              reducedMotion
                ? { opacity: 0 }
                : { opacity: 0, y: 26, scale: 0.985 }
            }
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={
              reducedMotion
                ? { opacity: 0 }
                : { opacity: 0, y: 10, scale: 0.99 }
            }
            transition={motionTokens.softSpring}
          >
            <div className="auth-glyph" aria-hidden="true">
              <span />
            </div>
            <p className="eyebrow">ALBUMROBOT</p>
            <h2 id="auth-title">群专辑库</h2>
            {authState === "checking" ? (
              <p className="auth-copy" role="status">
                正在检查访问权限…
              </p>
            ) : (
              <form onSubmit={submit}>
                <p className="auth-copy">输入群共享密码后继续。</p>
                <label htmlFor="group-password">群共享密码</label>
                <input
                  id="group-password"
                  type="password"
                  autoComplete="current-password"
                  autoFocus
                  value={password}
                  onChange={(event) => {
                    setPassword(event.target.value);
                    setError("");
                  }}
                  aria-invalid={Boolean(error)}
                  aria-describedby="auth-error"
                />
                <p id="auth-error" className="auth-error" role="alert">
                  {error}
                </p>
                <motion.button
                  type="submit"
                  disabled={!password || submitting}
                  whileTap={reducedMotion ? undefined : { scale: 0.985 }}
                  transition={motionTokens.micro}
                >
                  {submitting ? "正在验证…" : "继续"}
                </motion.button>
              </form>
            )}
          </motion.section>
        </motion.div>
      ) : null}
    </AnimatePresence>
  );
}

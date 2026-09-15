/* Template examples deliberately contain no guest photographs. */
/* eslint-disable @next/next/no-img-element */
import { useEffect, useState } from "react";
import { ArrowRightIcon } from "@phosphor-icons/react";
import { loadCompositionFonts, renderComposition } from "./compositor";
import { getTemplate, templates, previewLayout, previewArtworkStyle, type LayoutId, type TemplateId } from "./presets";
import styles from "./OnlinePhotobooth.module.css";

export function TemplateStep({ selectedId, onSelect, onContinue, kept, photographPreview }: {
  selectedId: LayoutId; onSelect: (id: TemplateId) => void; onContinue: () => void; kept: number;
  photographPreview?: { url?: string; ready: boolean; error: string; retry: () => void };
}) {
  const [images, setImages] = useState<Partial<Record<TemplateId, string>>>({});
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const selected = getTemplate(selectedId) ?? templates[0];
  const displayLayout = previewLayout(selected);
  // Never show a previous template's composition under the new template's geometry.
  const previewUrl = photographPreview ? (photographPreview.ready ? photographPreview.url : undefined) : images[selected.id];
  const previewError = photographPreview?.error || error;
  useEffect(() => {
    const abort = new AbortController();
    const urls: string[] = [];
    void loadCompositionFonts().then(async (fonts) => {
      const entries = await Promise.all(templates.map(async (template) => {
        const blob = await renderComposition({ layoutId: template.id, frameId: "ivory-v1", lookId: "naturale-v1", photos: [] }, fonts, abort.signal);
        if (abort.signal.aborted) return null;
        const url = URL.createObjectURL(blob); urls.push(url);
        return [template.id, url] as const;
      }));
      if (!abort.signal.aborted) { setImages(Object.fromEntries(entries.filter((entry) => entry !== null))); setError(""); }
    }).catch((cause: unknown) => {
      if (!abort.signal.aborted) setError(cause instanceof Error ? cause.message : "The templates could not be prepared.");
    });
    return () => { abort.abort(); urls.forEach((url) => URL.revokeObjectURL(url)); };
  }, [attempt]);
  return <>
    <div className={styles.templatePreview}>
      <p className={styles.previewLabel}>Your template preview</p>
      <div className={styles.templateMat} aria-busy={!previewUrl && !previewError}>
        {previewUrl ? <span className={styles.templatePortrait} style={{ aspectRatio: `${displayLayout.width} / ${displayLayout.height}` }}><span className={styles.previewArtwork} style={previewArtworkStyle(selected)}><img src={previewUrl} width={selected.width} height={selected.height} alt={`${selected.name}, ${photographPreview ? "your photographs" : "blank template preview"}`} /></span></span> : <p role="status">Preparing your template…</p>}
      </div>
      {previewError && <div role="alert"><p>{previewError}</p><button type="button" className={styles.textButton} onClick={() => { if (photographPreview?.error) photographPreview.retry(); else setAttempt((value) => value + 1); }}>Try again</button></div>}
    </div>
    <div className={styles.templateControls}>
      <fieldset className={styles.templateFieldset}>
        <legend>Select a template</legend>
        {templates.map((template) => <label key={template.id} className={`${styles.templateOption} ${selected.id === template.id ? styles.templateSelected : ""}`}>
          <span className={styles.templateThumbnail}>
            {images[template.id] && <span className={styles.templatePortraitThumb} style={{ aspectRatio: `${previewLayout(template).width} / ${previewLayout(template).height}` }}><span className={styles.previewArtwork} style={previewArtworkStyle(template)}><img src={images[template.id]} width={template.width} height={template.height} alt="" /></span></span>}
          </span>
          <span className={styles.templateCopy}><span className={styles.templateTitle}>{template.name}</span><span className={styles.templateDescription}>{template.description}</span><span className={styles.templateCount}>{template.slots.length} photographs</span></span>
          <input type="radio" name="template" value={template.id} aria-label={template.name} checked={selected.id === template.id} onChange={() => onSelect(template.id)} />
        </label>)}
      </fieldset>
      <div className={styles.templateAction}>
        <div aria-live="polite"><span className={styles.selectedEyebrow}>Selected template</span><p>{selected.name}<span>{selected.slots.length} photographs</span></p></div>
        <button type="button" className={styles.primary} onClick={onContinue} disabled={!images[selected.id]}>Continue to photographs<ArrowRightIcon size={18} aria-hidden="true" /></button>
      </div>
      {kept > 0 && <p className={styles.starterNote}>{kept} photographs are kept for you. Arrange them in the next step.</p>}
    </div>
  </>;
}

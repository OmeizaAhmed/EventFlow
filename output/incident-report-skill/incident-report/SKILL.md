---
name: incident-report
description: Write technical incident reports and LinkedIn debugging posts using a symptom, root cause, resolution, verification, and takeaway format. Also write matching captions or create before-and-after graphics when requested. Use for incident write-ups, not for fixing software unless separately requested.
---

# Incident report

Turn the incident into a credible first-person engineering story using the EventFlow post format. Default to LinkedIn-ready writing unless the user specifies another audience.

## Facts and status

Use the conversation and supplied evidence first. Identify the symptom, expected behavior, cause and mechanism, concrete fix, verification, and current status. Ask only for essential missing facts; omit optional unknowns. Never invent impact, downtime, test counts, timelines, or certainty. Distinguish a generated migration or prepared fix from one applied or deployed. Do not reuse the EventFlow incident's facts for other incidents. Keep secrets and private customer details out of public copy.

## Report format

Aim for 200–350 words, adjusted to complexity. Use short paragraphs, plain language, and a candid professional first-person voice. Follow this structure:

1. Open with the specific unexpected behavior and where it appeared.
2. The root cause: explain what caused it and why, including the relevant technical mechanism.
3. The resolution: explain the before-and-after change and corrective cleanup.
4. Verification and status: state observed checks and any remaining deployment or application steps.
5. My takeaway: close with one specific, reusable engineering lesson.
6. Add 3–5 relevant hashtags for LinkedIn.

Produce paste-ready plain text for LinkedIn: blank lines and simple labels, without Markdown heading markers, bold markers, fenced code blocks, or backtick-dependent formatting. Explain small code changes in prose. Use code blocks and more formal sections when requested for an internal report. Do not exaggerate severity or blame individuals.

## Caption mode

When asked for a caption, return only a caption, normally 120–180 words. Include a concrete hook, symptom, cause, fix, verified validation if available, and practical takeaway. Use short paragraphs, optionally one restrained emoji, and 3–5 relevant hashtags. Avoid generic engagement bait and repeating every detail in the graphic.

## Graphic mode

Create an image when requested, including when the user requests a full report-and-graphic package. Use the available imagegen skill and tool, reading its instructions first. If unavailable, explain the limitation and provide a labeled design brief; do not claim generation succeeded.

Preferred visual format:
- Square LinkedIn infographic with dark navy background, crisp typography, and generous margins.
- Incident-specific title and optional project-name eyebrow.
- Coral BEFORE and mint-green AFTER cards side by side, with short exact code snippets.
- One comparison row per meaningful change, ideally no more than two. Highlight changed lines.
- A concise cause-and-fix explanation beneath the cards.
- An optional corrective-action footer showing what is removed and retained.

Verify code punctuation, identifiers, legibility, and cropping in the generated image. Correct material errors before delivery. Label planned work without implying it has been applied. Adapt non-code incidents to a relevant visual comparison rather than inventing code.

## Scope

A report request produces the report; a caption request produces the caption; a full package request produces the report, caption, and graphic. Do not generate images for every report automatically. Drafting does not authorize publishing to LinkedIn or changing software or databases.

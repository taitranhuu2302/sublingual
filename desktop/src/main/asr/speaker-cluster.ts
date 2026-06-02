export interface SpeakerIdentity {
  id: string;
  label: string;
  color: string;
  centroid: Float64Array;
  lastSeenAt: number;
}

const SIMILARITY_THRESHOLD = 0.7;

const SPEAKER_COLORS = [
  "#FF6B6B", "#4ECDC4", "#FFE66D", "#6C5CE7",
  "#A8E6CF", "#FF8B94", "#B8B8D1", "#FFB347",
];

function cosineSimilarity(a: Float64Array, b: Float64Array): number {
  let dotProduct = 0;
  let normA = 0;
  let normB = 0;
  for (let i = 0; i < a.length; i++) {
    dotProduct += a[i] * b[i];
    normA += a[i] * a[i];
    normB += b[i] * b[i];
  }
  if (normA === 0 || normB === 0) return 0;
  return dotProduct / (Math.sqrt(normA) * Math.sqrt(normB));
}

function updateCentroid(cluster: SpeakerIdentity, embedding: Float64Array): void {
  const count = (cluster as any)._count ?? 1;
  const newCount = count + 1;
  (cluster as any)._count = newCount;
  for (let i = 0; i < cluster.centroid.length; i++) {
    cluster.centroid[i] = (cluster.centroid[i] * count + embedding[i]) / newCount;
  }
}

/**
 * Assign a speaker identity based on cosine similarity to existing clusters.
 * Returns a new or updated SpeakerIdentity.
 */
export function assignSpeaker(
  clusters: SpeakerIdentity[],
  embedding: Float64Array,
  maxSpeakers: number,
  now: number,
): SpeakerIdentity {
  let bestMatch: SpeakerIdentity | null = null;
  let bestScore = 0;

  for (const cluster of clusters) {
    const score = cosineSimilarity(embedding, cluster.centroid);
    if (score > bestScore && score >= SIMILARITY_THRESHOLD) {
      bestScore = score;
      bestMatch = cluster;
    }
  }

  if (bestMatch) {
    updateCentroid(bestMatch, embedding);
    bestMatch.lastSeenAt = now;
    return bestMatch;
  }

  if (clusters.length >= maxSpeakers) {
    let oldest = clusters[0];
    for (const c of clusters) {
      if (c.lastSeenAt < oldest.lastSeenAt) {
        oldest = c;
      }
    }
    const idx = clusters.indexOf(oldest);
    const newSpeaker: SpeakerIdentity = {
      id: oldest.id,
      label: oldest.label,
      color: oldest.color,
      centroid: new Float64Array(embedding),
      lastSeenAt: now,
    };
    (newSpeaker as any)._count = 1;
    clusters[idx] = newSpeaker;
    return newSpeaker;
  }

  const idx = clusters.length;
  const newSpeaker: SpeakerIdentity = {
    id: `spk_${idx + 1}`,
    label: `Speaker ${idx + 1}`,
    color: SPEAKER_COLORS[idx % SPEAKER_COLORS.length],
    centroid: new Float64Array(embedding),
    lastSeenAt: now,
  };
  (newSpeaker as any)._count = 1;
  clusters.push(newSpeaker);
  return newSpeaker;
}

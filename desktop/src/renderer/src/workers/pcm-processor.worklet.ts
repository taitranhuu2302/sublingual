class PCMProcessor extends AudioWorkletProcessor {
  private readonly targetSampleRate: number;
  private readonly chunkSize: number;
  private buffer: Float32Array;
  private bufferIndex: number;

  constructor(options?: AudioWorkletNodeOptions) {
    super();
    this.targetSampleRate =
      (options?.processorOptions?.targetSampleRate as number) ?? 16_000;
    this.chunkSize = (options?.processorOptions?.chunkSize as number) ?? 4_096;
    this.buffer = new Float32Array(this.chunkSize);
    this.bufferIndex = 0;
  }

  process(inputs: Float32Array[][]): boolean {
    const input = inputs[0];
    if (!input || input.length === 0) {
      return true;
    }

    const monoChannel = input[0];
    if (!monoChannel || monoChannel.length === 0) {
      return true;
    }

    const resampled = this.downsampleBuffer(
      monoChannel,
      sampleRate,
      this.targetSampleRate,
    );

    if (resampled.length === 0) {
      return true;
    }

    let sumSquares = 0;
    for (let i = 0; i < resampled.length; i += 1) {
      const sample = resampled[i];
      sumSquares += sample * sample;
      this.buffer[this.bufferIndex] = sample;
      this.bufferIndex += 1;

      if (this.bufferIndex >= this.chunkSize) {
        const int16Chunk = new Int16Array(this.chunkSize);
        for (let j = 0; j < this.chunkSize; j += 1) {
          const s = Math.max(-1, Math.min(1, this.buffer[j]));
          int16Chunk[j] = s < 0 ? s * 0x8000 : s * 0x7fff;
        }
        this.port.postMessage({
          type: 'pcm-chunk',
          payload: int16Chunk.buffer,
        });
        this.bufferIndex = 0;
      }
    }

    const rms = Math.sqrt(sumSquares / resampled.length);
    this.port.postMessage({
      type: 'rms',
      payload: rms,
    });

    return true;
  }

  private downsampleBuffer(
    input: Float32Array,
    inputSampleRate: number,
    outputSampleRate: number,
  ): Float32Array {
    if (outputSampleRate >= inputSampleRate) {
      return input;
    }
    const ratio = inputSampleRate / outputSampleRate;
    const newLength = Math.round(input.length / ratio);
    const result = new Float32Array(newLength);
    let offsetResult = 0;
    let offsetBuffer = 0;

    while (offsetResult < result.length) {
      const nextOffsetBuffer = Math.round((offsetResult + 1) * ratio);
      let accum = 0;
      let count = 0;
      for (
        let i = offsetBuffer;
        i < nextOffsetBuffer && i < input.length;
        i += 1
      ) {
        accum += input[i];
        count += 1;
      }
      result[offsetResult] = count > 0 ? accum / count : 0;
      offsetResult += 1;
      offsetBuffer = nextOffsetBuffer;
    }

    return result;
  }
}

registerProcessor('pcm-processor', PCMProcessor);


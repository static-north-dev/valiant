import { readFile, writeFile } from 'node:fs/promises';
import { deflateSync, inflateSync } from 'node:zlib';

const [inputPath, outputPath] = process.argv.slice(2);

if (!inputPath || !outputPath) {
  throw new Error('Usage: node remove-logo-white-background.mjs <input.png> <output.png>');
}

const source = await readFile(inputPath);
const signature = source.subarray(0, 8);
const chunks = [];
let offset = 8;

while (offset < source.length) {
  const length = source.readUInt32BE(offset);
  const type = source.subarray(offset + 4, offset + 8).toString('ascii');
  const data = source.subarray(offset + 8, offset + 8 + length);
  chunks.push({ type, data });
  offset += length + 12;
}

const header = chunks.find((chunk) => chunk.type === 'IHDR')?.data;
if (!header || header[8] !== 8 || header[9] !== 6 || header[12] !== 0) {
  throw new Error('Expected a non-interlaced 8-bit RGBA PNG.');
}

const width = header.readUInt32BE(0);
const height = header.readUInt32BE(4);
const bytesPerPixel = 4;
const stride = width * bytesPerPixel;
const compressed = Buffer.concat(chunks.filter((chunk) => chunk.type === 'IDAT').map((chunk) => chunk.data));
const filtered = inflateSync(compressed);
const pixels = Buffer.alloc(height * stride);

for (let row = 0; row < height; row += 1) {
  const filter = filtered[row * (stride + 1)];
  const rowStart = row * stride;
  const sourceStart = row * (stride + 1) + 1;

  for (let column = 0; column < stride; column += 1) {
    const value = filtered[sourceStart + column];
    const left = column >= bytesPerPixel ? pixels[rowStart + column - bytesPerPixel] : 0;
    const above = row > 0 ? pixels[rowStart - stride + column] : 0;
    const upperLeft = row > 0 && column >= bytesPerPixel ? pixels[rowStart - stride + column - bytesPerPixel] : 0;
    let decoded = value;

    if (filter === 1) decoded = (value + left) & 255;
    if (filter === 2) decoded = (value + above) & 255;
    if (filter === 3) decoded = (value + Math.floor((left + above) / 2)) & 255;
    if (filter === 4) {
      const prediction = left + above - upperLeft;
      const leftDistance = Math.abs(prediction - left);
      const aboveDistance = Math.abs(prediction - above);
      const upperLeftDistance = Math.abs(prediction - upperLeft);
      decoded = (value + (leftDistance <= aboveDistance && leftDistance <= upperLeftDistance ? left : aboveDistance <= upperLeftDistance ? above : upperLeft)) & 255;
    }

    pixels[rowStart + column] = decoded;
  }
}

for (let index = 0; index < pixels.length; index += bytesPerPixel) {
  if (pixels[index] === 255 && pixels[index + 1] === 255 && pixels[index + 2] === 255) {
    pixels[index + 3] = 0;
  }
}

const scanlines = Buffer.alloc(height * (stride + 1));
for (let row = 0; row < height; row += 1) {
  pixels.copy(scanlines, row * (stride + 1) + 1, row * stride, (row + 1) * stride);
}

const crcTable = Array.from({ length: 256 }, (_, index) => {
  let value = index;
  for (let bit = 0; bit < 8; bit += 1) value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
  return value >>> 0;
});
const crc32 = (buffer) => {
  let value = 0xffffffff;
  for (const byte of buffer) value = crcTable[(value ^ byte) & 255] ^ (value >>> 8);
  return (value ^ 0xffffffff) >>> 0;
};
const chunk = (type, data) => {
  const typeBuffer = Buffer.from(type);
  const result = Buffer.alloc(data.length + 12);
  result.writeUInt32BE(data.length, 0);
  typeBuffer.copy(result, 4);
  data.copy(result, 8);
  result.writeUInt32BE(crc32(Buffer.concat([typeBuffer, data])), result.length - 4);
  return result;
};

await writeFile(outputPath, Buffer.concat([signature, chunk('IHDR', header), chunk('IDAT', deflateSync(scanlines)), chunk('IEND', Buffer.alloc(0))]));

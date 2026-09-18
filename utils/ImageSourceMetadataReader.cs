using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace AutoNai3Tools.utils {
    internal static class ImageSourceMetadataReader {
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        internal static bool TryReadGenerationMetadata(string imagePath, out JObject metadata, out string sourceLocation,
            out string errorMessage) {
            metadata = null;
            sourceLocation = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(imagePath)) {
                errorMessage = "图片路径为空。";
                return false;
            }

            if (!File.Exists(imagePath)) {
                errorMessage = "图片文件不存在。";
                return false;
            }

            if (!HasPngSignature(imagePath)) {
                errorMessage = "当前仅支持 PNG 源数据读取。";
                return false;
            }

            List<TextChunk> chunks;
            try {
                chunks = ReadPngTextChunks(imagePath);
            }
            catch (Exception ex) {
                errorMessage = $"读取 PNG 文本块失败: {ex.Message}";
                return false;
            }

            if (chunks == null || chunks.Count == 0) {
                errorMessage = "未找到 PNG 文本元数据。";
                return false;
            }

            foreach (string keyword in new[] { "Comment", "comment", "parameters", "Description", "description", "Source", "source" }) {
                var chunk = chunks.FirstOrDefault(c => string.Equals(c.Keyword, keyword, StringComparison.OrdinalIgnoreCase));
                if (chunk == null || string.IsNullOrWhiteSpace(chunk.Value))
                    continue;

                if (!TryExtractJsonObject(chunk.Value, out string jsonText))
                    continue;

                try {
                    var parsed = JObject.Parse(jsonText);
                    if (parsed["prompt"] != null || parsed["steps"] != null || parsed["sampler"] != null) {
                        metadata = parsed;
                        sourceLocation = $"PNG {chunk.ChunkType}/{chunk.Keyword}";
                        return true;
                    }
                }
                catch {
                    // ignore invalid json candidate and continue.
                }
            }

            errorMessage = "已找到文本块，但未识别到可用生成参数 JSON。";
            return false;
        }

        private static bool HasPngSignature(string path) {
            try {
                using (var stream = File.OpenRead(path)) {
                    if (stream.Length < PngSignature.Length)
                        return false;

                    byte[] header = new byte[PngSignature.Length];
                    int read = stream.Read(header, 0, header.Length);
                    return read == header.Length && header.SequenceEqual(PngSignature);
                }
            }
            catch {
                return false;
            }
        }

        private static List<TextChunk> ReadPngTextChunks(string path) {
            var chunks = new List<TextChunk>();
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream)) {
                byte[] signature = reader.ReadBytes(PngSignature.Length);
                if (!signature.SequenceEqual(PngSignature))
                    throw new InvalidDataException("不是有效 PNG 文件。");

                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length) {
                    uint length = ReadUInt32BigEndian(reader);
                    string chunkType = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    byte[] data = reader.ReadBytes(checked((int)length));
                    reader.ReadBytes(4); // CRC

                    if ("tEXt".Equals(chunkType, StringComparison.Ordinal)) {
                        if (TryParseTextChunk(data, out string keyword, out string value))
                            chunks.Add(new TextChunk(chunkType, keyword, value));
                    }
                    else if ("iTXt".Equals(chunkType, StringComparison.Ordinal)) {
                        if (TryParseInternationalTextChunk(data, out string keyword, out string value))
                            chunks.Add(new TextChunk(chunkType, keyword, value));
                    }
                    else if ("zTXt".Equals(chunkType, StringComparison.Ordinal)) {
                        if (TryParseCompressedTextChunk(data, out string keyword, out string value))
                            chunks.Add(new TextChunk(chunkType, keyword, value));
                    }

                    if ("IEND".Equals(chunkType, StringComparison.Ordinal))
                        break;
                }
            }

            return chunks;
        }

        private static bool TryParseTextChunk(byte[] data, out string keyword, out string value) {
            keyword = null;
            value = null;
            int sep = Array.IndexOf(data, (byte)0);
            if (sep <= 0 || sep >= data.Length - 1)
                return false;

            keyword = Encoding.GetEncoding("ISO-8859-1").GetString(data, 0, sep);
            value = Encoding.UTF8.GetString(data, sep + 1, data.Length - sep - 1);
            return true;
        }

        private static bool TryParseInternationalTextChunk(byte[] data, out string keyword, out string value) {
            keyword = null;
            value = null;
            int pos = 0;

            int keywordEnd = IndexOfNull(data, pos);
            if (keywordEnd < 0)
                return false;
            keyword = Encoding.GetEncoding("ISO-8859-1").GetString(data, pos, keywordEnd - pos);
            pos = keywordEnd + 1;

            if (pos + 2 > data.Length)
                return false;

            byte compressionFlag = data[pos++];
            pos++; // compression method

            int languageEnd = IndexOfNull(data, pos);
            if (languageEnd < 0)
                return false;
            pos = languageEnd + 1;

            int translatedEnd = IndexOfNull(data, pos);
            if (translatedEnd < 0)
                return false;
            pos = translatedEnd + 1;

            if (pos > data.Length)
                return false;

            byte[] textBytes = data.Skip(pos).ToArray();
            if (compressionFlag == 1) {
                textBytes = DecompressZlib(textBytes);
            }

            value = Encoding.UTF8.GetString(textBytes);
            return true;
        }

        private static bool TryParseCompressedTextChunk(byte[] data, out string keyword, out string value) {
            keyword = null;
            value = null;

            int keywordEnd = IndexOfNull(data, 0);
            if (keywordEnd < 0 || keywordEnd + 2 > data.Length)
                return false;

            keyword = Encoding.GetEncoding("ISO-8859-1").GetString(data, 0, keywordEnd);
            int compressionMethodIndex = keywordEnd + 1;
            byte compressionMethod = data[compressionMethodIndex];
            if (compressionMethod != 0)
                return false;

            byte[] compressedText = data.Skip(compressionMethodIndex + 1).ToArray();
            byte[] rawText = DecompressZlib(compressedText);
            value = Encoding.UTF8.GetString(rawText);
            return true;
        }

        private static byte[] DecompressZlib(byte[] compressed) {
            using (var input = new MemoryStream(compressed))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream()) {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        private static uint ReadUInt32BigEndian(BinaryReader reader) {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length < 4)
                throw new EndOfStreamException();

            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static int IndexOfNull(byte[] data, int startIndex) {
            for (int i = startIndex; i < data.Length; i++) {
                if (data[i] == 0)
                    return i;
            }

            return -1;
        }

        private static bool TryExtractJsonObject(string rawText, out string jsonText) {
            jsonText = null;
            if (string.IsNullOrWhiteSpace(rawText))
                return false;

            string text = rawText.Trim();
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
                return false;

            jsonText = text.Substring(start, end - start + 1);
            return true;
        }

        private sealed class TextChunk {
            internal TextChunk(string chunkType, string keyword, string value) {
                ChunkType = chunkType;
                Keyword = keyword;
                Value = value;
            }

            internal string ChunkType { get; }
            internal string Keyword { get; }
            internal string Value { get; }
        }
    }
}

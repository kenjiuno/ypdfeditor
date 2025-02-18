using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace yPDFEditor.Utils
{
    public class PdfEdit
    {
        public Action UpdateTitle;
        public ObservableCollection<ThumbSet> Picts;

        private string fpCurrent = null;
        private MemoryStream pdfMem = new MemoryStream();
        private bool isModified = false;
        private PdfDocument pdfDoc = null;

        public void Close()
        {
            Currentfp = null;
            pdfDoc?.Dispose();
            pdfDoc = null;
            Modified = false;
        }

        public bool Modified
        {
            get
            {
                return isModified;
            }
            set
            {
                isModified = value;
                UpdateTitle();
            }
        }

        public string Currentfp
        {
            get
            {
                return fpCurrent;
            }
            set
            {
                fpCurrent = value;
                UpdateTitle();
            }
        }

        public bool IsOpened => pdfDoc != null;

        public int PageCount => pdfDoc?.PageCount ?? 0;

        public void OpenFile(string file)
        {
            pdfDoc?.Dispose();
            pdfMem?.Dispose();

            using (var fs = File.OpenRead(file))
            {
                pdfMem = new MemoryStream();
                fs.CopyTo(pdfMem);
                pdfMem.Position = 0;
            }

            pdfDoc = PdfDocument.Load(pdfMem);

            Picts.Clear();
            Currentfp = null;
            int cnt = pdfDoc.PageCount;
            for (int i = 0; i < cnt; i++)
            {
                Picts.Add(ThumbSet.Delayed);
            }
            Currentfp = file;
            Modified = false;
        }

        /// <summary>
        /// サムネイル画像を作成
        /// </summary>
        /// <param name="pageIndex">0 ~ N-1</param>
        /// <param name="maxWidth"></param>
        /// <param name="maxHeight"></param>
        /// <returns></returns>
        public Bitmap GetThumbnailOf(int pageIndex, int maxWidth, int maxHeight)
        {
            var pageSize = pdfDoc.PageSizes[pageIndex];
            pageSize.Width *= 2;
            pageSize.Height *= 2;
            var rect = FitRect3.Fit(
                new Rectangle(0, 0, Math.Max(16, maxWidth), Math.Max(16, maxHeight)),
                Size.Truncate(pageSize)
            );
            var cx = rect.Width;
            var cy = rect.Height;
            var bitmap = (Bitmap)pdfDoc.Render(pageIndex, cx, cy, 96, 96, PdfRenderFlags.Annotations);
            return bitmap;
        }

        /// <summary>
        /// ページを削除
        /// </summary>
        /// <param name="firstIdx">0 to N-1</param>
        /// <param name="lastIdx">0 to N-1</param>
        public void DeletePages(int firstIdx, int lastIdx)
        {
            for (int x = firstIdx; x <= lastIdx; x++)
            {
                pdfDoc.DeletePage(firstIdx);
                Picts.RemoveAt(firstIdx);
            }

            if (Picts.Any() || Currentfp != null)
            {
                Modified = true;
            }
            else
            {
                Modified = false;
            }
        }

        /// <summary>
        /// 単一ページの PDF を取得
        /// </summary>
        /// <param name="pageIdx">0 to N-1</param>
        /// <returns></returns>
        public byte[] GetPDF(int pageIdx)
        {
            using (var os = new MemoryStream())
            {
                using (var newDoc = PdfDocument.Compose(pdfDoc, $"{1 + pageIdx}"))
                {
                    newDoc.Save(os);
                }
                return os.ToArray();
            }
        }

        /// <summary>
        /// ページを挿入
        /// </summary>
        /// <param name="pdfFile">ソース</param>
        /// <param name="destIdx">0 to N</param>
        public void InsertPages(Stream pdfFile, int destIdx)
        {
            using (var pdfIn = PdfDocument.Load(pdfFile))
            {
                var numInPages = pdfIn.PageCount;
                MakesureDoc();
                pdfDoc.ImportPages(pdfIn, null, destIdx);

                Modified = true;

                for (int i = 0; i < numInPages; i++, destIdx++)
                {
                    Picts.Insert(destIdx, ThumbSet.Delayed);
                }
            }
        }

        /// <summary>
        /// PDF を挿入
        /// </summary>
        /// <param name="file">ソース</param>
        /// <param name="destIdx">0 to N</param>
        public void InsertPDF(string file, int destIdx)
        {
            using (var pdfIn = PdfDocument.Load(file))
            {
                var numInPages = pdfIn.PageCount;
                MakesureDoc();
                pdfDoc.ImportPages(pdfIn, null, destIdx);

                Modified = true;

                for (int i = 0; i < numInPages; i++, destIdx++)
                {
                    Picts.Insert(destIdx, ThumbSet.Delayed);
                }
            }
        }

        /// <summary>
        /// ページを移動
        /// </summary>
        /// <param name="destIdx">0 to N</param>
        /// <param name="firstIdx">0 to N-1</param>
        /// <param name="lastIdx">0 to N-1</param>
        public void MovePages(int destIdx, int firstIdx, int lastIdx)
        {
            var numPages = Picts.Count;
            var pageNums = new List<int>();
            for (int x = 0; x <= numPages; x++)
            {
                if (destIdx == x)
                {
                    for (int y = firstIdx; y <= lastIdx; y++)
                    {
                        pageNums.Add(1 + y);
                    }
                }
                if (x < Picts.Count && (x < firstIdx || lastIdx < x))
                {
                    pageNums.Add(1 + x);
                }
            }

            EditSetPages(pageNums.ToArray());
        }

        /// <summary>
        /// ページを再配列
        /// </summary>
        /// <param name="indices">0 to N-1 の配列</param>
        public void EditSetPages(int[] indices)
        {
            var numPages = pdfDoc.PageCount;

            pdfDoc.CopyPages(
                string.Join(
                    ",",
                    indices
                        .Select(it => it.ToString())
                ),
                numPages
            );

            for (int x = 0; x < numPages; x++)
            {
                pdfDoc.DeletePage(0);
            }

            var array = new ThumbSet[indices.Length];

            for (int x = 0; x < indices.Length; x++)
            {
                array[x] = Picts[indices[x] - 1];
            }

            {
                int x;
                for (x = 0; x < array.Length; x++)
                {
                    if (x < Picts.Count)
                    {
                        Picts[x] = array[x];
                    }
                    else
                    {
                        Picts.Add(array[x]);
                    }
                }
                for (; x < Picts.Count; x++)
                {
                    Picts.RemoveAt(Picts.Count - 1);
                }
            }

            Modified = true;
        }

        /// <summary>
        /// ページを複写
        /// </summary>
        /// <param name="destIdx">0 to N</param>
        /// <param name="firstIdx">0 to N-1</param>
        /// <param name="lastIdx">0 to N-1</param>
        public void CopyPages(int destIdx, int firstIdx, int lastIdx)
        {
            var pageNums = new List<int>();
            for (int x = 0; x <= Picts.Count; x++)
            {
                if (destIdx == x)
                {
                    for (int y = firstIdx; y <= lastIdx; y++)
                    {
                        pageNums.Add(1 + y);
                    }
                }
                if (x < Picts.Count)
                {
                    pageNums.Add(1 + x);
                }
            }

            EditSetPages(pageNums.ToArray());
        }

        /// <summary>
        /// ページを回転
        /// </summary>
        /// <param name="firstIdx">0 to N-1</param>
        /// <param name="lastIdx">0 to N-1</param>
        /// <param name="rotateLeft"></param>
        public void RotatePages(int firstIdx, int lastIdx, bool rotateLeft)
        {
            for (int x = 0; x < Picts.Count; x++)
            {
                if (firstIdx <= x && x <= lastIdx)
                {
                    pdfDoc.RotatePage(
                        x,
                        (PdfRotation)(((int)pdfDoc.GetPageRotation(x) + (rotateLeft ? -1 : 1)) & 3)
                    );

                    Picts[x] = ThumbSet.Delayed;

                    Modified = true;
                }
            }
        }

        public void SaveTo(string saveTo)
        {
            var fpbak = Path.ChangeExtension(saveTo, ".bak");
            if (File.Exists(fpbak))
            {
                File.Delete(fpbak);
            }
            if (File.Exists(saveTo))
            {
                File.Copy(saveTo, fpbak);
            }

            using (var pdfOut = PdfDocument.Compose(pdfDoc, null))
            using (var writeTo = File.Open(saveTo, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                writeTo.SetLength(0);
                pdfOut.Save(writeTo);
            }

            Currentfp = saveTo;
            Modified = false;

            if (File.Exists(fpbak))
            {
                File.Delete(fpbak);
            }
        }

        public void AppendPages(string file)
        {
            using (var pdfIn = PdfDocument.Load(file))
            {
                var numInPages = pdfIn.PageCount;

                MakesureDoc();

                var numPages = pdfDoc.PageCount;

                pdfDoc.ImportPages(pdfIn, null, numPages);

                for (int i = 0; i < numInPages; i++)
                {
                    Picts.Add(ThumbSet.Delayed);
                }
            }

            Modified = true;
        }

        private void MakesureDoc()
        {
            if (pdfDoc == null)
            {
                pdfDoc = PdfDocument.CreateEmpty();
            }
        }

        /// <summary>
        /// 範囲を指定して PDF へエクスポート
        /// </summary>
        /// <param name="saveTo">保存先ファイルパス</param>
        /// <param name="pageRange">`1,2-3` and so on, or null for entire</param>
        public void Export(string saveTo, string pageRange)
        {
            using (var pdfOut = PdfDocument.Compose(pdfDoc, pageRange))
            {
                pdfOut.Save(saveTo);
            }
        }
    }
}

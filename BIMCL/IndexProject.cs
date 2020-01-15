using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml.Serialization;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Formatters.Binary;
//using iTextSharp.text.pdf;
//using iTextSharp.text.pdf.parser;
using iText;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using System.Diagnostics;

namespace BookIndexMaker
{
    public delegate void UpdateEventHandler(object sender, UpdateEventArgs e);

    public class UpdateEventArgs : EventArgs
    {
        public IndexProject.EventUpdateTypes eventType { get; set; }
        public string message { get; set; }
        public int progressMax { get; set; }
        public int progressCurrent { get; set; }
        public int warningCount { get; set; }
    }

    [Serializable]
    public class IndexProject
    {
        public enum DocumentTypes
        {            
            PDF
        }

        public enum EventUpdateTypes
        {
            SavingToFile,
            LoadingFromFile,
            ParsingFromWord,
            ParsingFromPDF,
            Indexing,
            LoadingDependencies,
            DictionarySearch,
            Error
        }

        public event EventHandler<UpdateEventArgs> OnUpdate;

        public List<BookPage> PagesInBook { get; set; }
        public List<WordOnPage> RawWordList { get; set; }
        public List<WordOnPage> FilteredWordList { get; set; }
        public List<string> Warnings { get; set; }
        public Filters Filter { get; set; }
        public bool IsFiltered { get; set; }

        public List<DictionaryWord> DictionaryWords { get; set; }

        public IndexProject()
        {
            IsFiltered = false;
            Filter = new Filters();
            PagesInBook = new List<BookPage>();
            RawWordList = new List<WordOnPage>();
            FilteredWordList = new List<WordOnPage>();
            Warnings = new List<string>();
            DictionaryWords = new List<DictionaryWord>();
        }

        public List<string> GetWordList()
        {
            List<string> list = new List<string>();

            foreach (WordOnPage wop in RawWordList)
            {
                list.Add(wop.Word);
            }

            return list;
        }

        public List<string> GetAutoCompleteList()
        {
            List<string> list = new List<string>();
            
            foreach (WordOnPage wop in RawWordList)
            {
                list.Add(wop.Word);
            }

            return list;
        }

        public async Task<bool> ProcessIt(Stream DocumentToLoad, DocumentTypes docType, bool DictionaryMatch, bool ExcludeNumberWords)
        {
            var ret = true;

            await Task.Run(async () =>
            {
                try
                {

                    int PageNum = 0;

                    if (docType == DocumentTypes.PDF)
                    {

                        PagesInBook = new List<BookPage>();

                        using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(DocumentToLoad)))
                        {
                            int maxPages = pdfDoc.GetNumberOfPages();
                            PageNum = pdfDoc.GetNumberOfPages();

                            StringBuilder text = new StringBuilder();

                            for (int x = 1; x <= pdfDoc.GetNumberOfPages(); x++)
                            {
                                FireUpdateEvent(EventUpdateTypes.ParsingFromPDF, "Page: " + x.ToString(), maxPages, x, Warnings.Count());

                                PagesInBook.Add(new BookPage(PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(x))));
                            }
                        }

                    }

                    List<WordOnPage> index = new List<WordOnPage>();

                    int pageCount = 0;
                    int pageTotal = PagesInBook.Count();
                    // Loop through all of the pages
                    foreach (BookPage bp in PagesInBook)
                    {
                        pageCount++;

                        FireUpdateEvent(EventUpdateTypes.Indexing, "Page: " + pageCount.ToString(), pageTotal, pageCount, Warnings.Count());

                        // Loop through all of the Words on the page
                        foreach (string word in bp.Words)
                        {
                            bool foundInIndex = false;

                            bool doit = true;

                            if (ContainsNumber(word) && ExcludeNumberWords) doit = false;

                            if (doit)
                            {

                                // Loop through our existing Index
                                foreach (WordOnPage wop in index.Where(x => x.Word.ToLower().Trim() == word.ToLower().Trim()))
                                {
                                    // the Word is found in the index

                                    foundInIndex = true;
                                    bool pageAlreadyMarked = false;

                                    // Loop through the pages this word is found on
                                    foreach (PageClass page in wop.Page.Where(p => p.Page == pageCount))
                                    {
                                        pageAlreadyMarked = true;
                                        break;
                                    }

                                    // If this page was not marked for this word
                                    if (!pageAlreadyMarked)
                                    {
                                        // add the page mark
                                        wop.Page.Add(new PageClass(pageCount));
                                    }

                                    break;

                                }

                                // If the word was not found in the index
                                if (!foundInIndex)
                                {
                                    WordOnPage nwop = new WordOnPage();
                                    nwop.Word = word.Trim();
                                    //nwop.WordType = GetWordType(nwop.Word);
                                    nwop.Page = new List<PageClass>();
                                    nwop.Page.Add(new PageClass(pageCount));
                                    index.Add(nwop);
                                }
                            }
                        }
                    }

                    RawWordList = index.OrderBy(p => p.Word).ToList();

                    int c = 0;

                    if (DictionaryMatch)
                    {

                        foreach (WordOnPage wop in RawWordList)
                        {
                            c++;

                            FireUpdateEvent(EventUpdateTypes.DictionarySearch, wop.Word, RawWordList.Count, c, Warnings.Count());

                            bool found = false;
                            foreach (DictionaryWord dic in DictionaryWords.Where(x => x.Word.ToLower().Trim() == wop.Word.ToLower().Trim()))
                            {
                                wop.WordType = dic.WordType;
                                found = true;
                                break;
                            }

                            if (!found)
                            {
                                wop.WordType = "Unknown";
                            }

                        }
                    }

                    List<WordOnPage> finalList = new List<WordOnPage>();

                    StringBuilder stb = new StringBuilder();
                    foreach (WordOnPage wop in RawWordList)
                    {

                        //if (wop.)
                        //{

                        //    finalList.Add(wop);
                        //    //StringBuilder sbpages = new StringBuilder();
                        //    //foreach (int page in wop.Page)
                        //    //{
                        //    //    sbpages.Append(page.ToString() + ", ");
                        //    //}
                        //    //stb.AppendLine(wop.Word + " - " + sbpages.ToString());
                        //}

                    }

                   // Serialize(ProjectFileToCreate, this);

                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ERROR:" + ex.Message);
                    FireUpdateEvent(EventUpdateTypes.Error, ex.Message, 0, 0, 0);
                }
            });

            return ret;
        }

        private byte[] ReadToEnd(Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }

        public void FireUpdateEvent(EventUpdateTypes eventType, string message, int progressMax, int progressCurrent, int warningCount)
        {

            UpdateEventArgs e = new UpdateEventArgs();
            e.eventType = eventType;
            e.message = message;
            e.progressMax = progressMax;
            e.progressCurrent = progressCurrent;
            e.warningCount = warningCount;
            //OnUpdate(this, e);
            Debug.WriteLine(e.message + ": " + e.progressCurrent + "/" + e.progressMax);
        }


        /// <summary>
        /// Serializes the Project in an Obfuscated Manner
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="objectToSerialize"></param>
        public void Serialize(string filename, IndexProject objectToSerialize)
        {
            Stream stream = File.Open(filename, FileMode.Create);
            BinaryFormatter bFormatter = new BinaryFormatter();
            bFormatter.Serialize(stream, objectToSerialize);
            stream.Close();
        }

        /// <summary>
        /// Deserializes the Project 
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static IndexProject DeSerialize(string filename)
        {
            IndexProject objectToSerialize;
            Stream stream = File.Open(filename, FileMode.Open);
            BinaryFormatter bFormatter = new BinaryFormatter();
            objectToSerialize = (IndexProject)bFormatter.Deserialize(stream);
            stream.Close();
            return objectToSerialize;
        }

        /// <summary>
        /// Deserializes a List of BookPage
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static List<BookPage> DeSerializeBookPageList(string filename)
        {
            List<BookPage> objectToSerialize;
            Stream stream = File.Open(filename, FileMode.Open);
            BinaryFormatter bFormatter = new BinaryFormatter();
            objectToSerialize = (List<BookPage>)bFormatter.Deserialize(stream);
            stream.Close();
            return objectToSerialize;
        }

        private bool ContainsNumber(string what)
        {
            return Regex.IsMatch(what, @"\d", RegexOptions.IgnoreCase);
        }

        private string GetWordType(string word)
        {
            string type = "Unknown";

            var wordMatch = from dw in DictionaryWords
                            where dw.Word == word.ToLower().Trim()
                            select dw;

            bool found = false;
            foreach (DictionaryWord dic in wordMatch)
            {
                type = dic.WordType;
                found = true;
                break;
            }

            if (!found)
            {
                type = "Unknown";
            }

            return type;
        }

    }

}

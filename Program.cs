using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Verygood.Biz.Common.Log.Object;
using Verygood.Biz.Common.Log.Logic;
using System.Net;
using System.Diagnostics;
using log4net;
using Verygood.Biz;
using System.Data.Common;
using System.Data.SqlClient;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using Verygood.bizbase;
using System.Text;

namespace ImageClob
{
    public class ImageClob 
    {
        private static ILog iLog = log4net.LogManager.GetLogger("LogFile");
        private static string LogFIlePath = "";
        private static string NowDateString = "";
        private static int cnt = 0;
        private static int lcnt = 0;
        private static int lcnt3= 0;
        private static int test1 = 0;


        public class InfoFileTypeRS : Verygood.bizbase.Biz_Base
        {
            /// <summary>
            /// 파일코드
            /// </summary>
            [DatabaseAttribute("FILE_CODE")]
            public int FileCode { get; set; }

            /// <summary>
            /// 정보구분코드
            /// </summary>
            [DatabaseAttribute("FILE_DIR")]
            public string FILE_DIR { get; set; }

            /// <summary>
            /// 정보구분코드
            /// </summary>
            [DatabaseAttribute("FILE_PATH")]
            public string FILE_PATH { get; set; }
        }


        static void Main(string[] args)
        {
            LogFIlePath = "D:\\LogFiles\\ImageSize_batch\\ImageSize_batch.txt";
            LogFIlePath = LogFIlePath.Replace(".txt", "_" + NowDateString + ".txt");

            Imageclogs();

            WriteLog(string.Format("---------------이미지 변환 종료---------------"));
        }

        /// <summary>
        /// 
        /// </summary>
        public static void Imageclogs()
        {

            ImageClob clob = new ImageClob();
            string sql = " SELECT a.file_code,'D:\\Content\\' + Concat(a.region_code + '\\', a.nation_code + '\\', a.state_code + '\\', a.city_code) + '\\image\\' AS FILE_DIR, " +
                "'D:\\Content\\' + Concat(a.region_code + '\\', a.nation_code + '\\', a.state_code + '\\', a.city_code +'\\image\\', CONVERT(NVARCHAR, a.file_code) + '.', a.extension_name ) AS FILE_PATH" +
                " FROM   inf_file_master a WHERE FILE_CODE IN(  SELECT DISTINCT( main_file_code ) FROM pkg_master WHERE SHOW_YN = 'Y') AND  a.file_name IN('34721', '575765', '741135', '1829918', '1847034')";

            Verygood.bizbase.Biz_Base biz = new Verygood.bizbase.Biz_Base();
            List<InfoFileTypeRS> infofilelist = new List<InfoFileTypeRS>();
            int cnt = 1;
            int errocnt = 1;

            using (DbCommand cmd = biz.Db.GetSqlStringCommand(sql))
            {
                infofilelist = biz.GetListClass<InfoFileTypeRS>(cmd);
            }

            WriteLog(string.Format("------------ 변환이미지 총갯수 {0} 이미지 변환 시작 ---------------", infofilelist.Count()));

            foreach (InfoFileTypeRS ds in infofilelist)
            {
                // string file = OptimizeImage(ds.FILE_PATH, null, 1024, 480, false, 85);
                
                try
                {
                    bool success = resizeImage(Convert.ToString(ds.FileCode), ds.FILE_DIR, ds.FILE_PATH, null);

                    if (success)
                    {
                        string updatesql = string.Format("UPDATE INF_FILE_MASTER SET  FILE_NAME_W = '{0}_0.jpg'  WHERE FILE_NAME =  '{1}'", ds.FileCode, ds.FileCode);
                        using (DbCommand cmd = biz.Db.GetSqlStringCommand(updatesql))
                        {
                            biz.Db.ExecuteNonQuery(cmd);
                        }
                        WriteLog(string.Format("이미지 변환 파일명:  {0}/[{1}/{2}] ---------------", ds.FileCode, cnt++, infofilelist.Count()));
                    }
                    else
                    {
                        WriteLog(string.Format("---------------[Error] 이미지변환 Error [{0}/{1}]---------------",  errocnt++, infofilelist.Count()));

                    }


                }
                catch (Exception ex)
                {
                    WriteLog(string.Format("---------------[Error] Update Error 내용 : {0}/{1}---------------", ex.ToString(), errocnt++));
                    throw ex;
                }

            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static bool resizeImage(string filename, string filedir, string imagefile, FileStream targetStream)
        {
            try
            {
                using (System.Drawing.Image sourceImage = (targetStream == null) ? System.Drawing.Image.FromFile(imagefile) : System.Drawing.Image.FromStream(targetStream))
                {
                    if (sourceImage != null)
                    {
                        long file_size = new FileInfo(imagefile).Length;
                        string FileSize = HumanReadableFileSize(file_size);

                        string new_file_0 = string.Format("{0}{1}_0.jpg", filedir, filename); //와이드 이미지 _0

                        new FileInfo(new_file_0).Delete();//_0와이드파일지우기

                        WriteLog(string.Format("이미지 변환 파일 사이즈:  {0} ---------------", FileSize));

                        if (sourceImage.Width < 1048 && sourceImage.Height < 480)  //가로세로 작을경우
                        {

                            float nPercentW = ((float)1048 / (float)sourceImage.Width);
                            int destHeight = (int)(sourceImage.Height * nPercentW);
                            string file = OptimizeImage(filename, filedir, imagefile, null, 1048, destHeight, true);

                            //Bitmap resizeImage = new Bitmap(sourceImage);
                            //resizeImage = resizeImage.Clone(new Rectangle(0, 0, 1048, (int)destHeight), resizeImage.PixelFormat);//작으면 상관없이 그냥
                            //resizeImage.Save(string.Format("{0}{1}_0.jpg", filedir, filename)); //와이드변환 
                        }

                        else if (sourceImage.Width > 1048 && sourceImage.Height > 480) //가로세로 클경우
                        {
                            float nPercentW = ((float)1048 / (float)sourceImage.Width);
                            int destHeight = (int)(sourceImage.Height * nPercentW);

                            //이미지변환
                            string file = OptimizeImage(filename, filedir, imagefile, null, 1048, destHeight, true);

                            //새로운이미지로
                            //System.Drawing.Image newimage = System.Drawing.Image.FromFile(file);


                            //Bitmap croppedBitmap = new Bitmap(newimage); //새로생성된 이미지
                            ////croppedBitmap = croppedBitmap.Clone(new Rectangle((newimage.Width - 1048) / 2, (newimage.Height - 480) / 2, 1024, 480), croppedBitmap.PixelFormat);//정위치.
                            //croppedBitmap = croppedBitmap.Clone(new Rectangle(0, 0, 1048, destHeight), croppedBitmap.PixelFormat);//작으면 상관없이 그냥
                            //croppedBitmap.Save(string.Format("{0}{1}_0.jpg", filedir, filename)); //와이드변환

                            //newimage.Dispose();
                            //new FileInfo(file).Delete();

                        }
                        else if (sourceImage.Width > 1048 && sourceImage.Height < 480) //가로 크고 세로 작은 경우
                        {

                            float nPercentW = ((float)480 / (float)sourceImage.Height);
                            int destwidth =(int)(sourceImage.Width * nPercentW);

                            string file = OptimizeImage(filename, filedir, imagefile, null, destwidth, 480, true);

                            //Bitmap croppedBitmap = new Bitmap(sourceImage);
                            //croppedBitmap = croppedBitmap.Clone(new Rectangle(0, 0, (int)destwidth, 480), croppedBitmap.PixelFormat);//작으면 상관없이 그냥
                            //croppedBitmap.Save(string.Format("{0}{1}_0.jpg", filedir, filename)); //와이드변환
                        }
                        else if (sourceImage.Width < 1048 && sourceImage.Height > 480) //가로 작고 세로 큰 경우
                        {

                            float nPercentW = ((float)1480 / (float)sourceImage.Width);
                            int destwidth = (int)(sourceImage.Width * nPercentW);

                            string file = OptimizeImage(filename, filedir, imagefile, null, destwidth, 1110, true);

                            //Bitmap resizeImage = new Bitmap(sourceImage);
                            //resizeImage = resizeImage.Clone(new Rectangle(0, 0, (int)destwidth, 1110), resizeImage.PixelFormat);//작으면 상관없이 그냥
                            //resizeImage.Save(string.Format("{0}{1}_0.jpg", filedir, filename)); //와이드변환 
                        }
                        cnt++;
                    }
                    sourceImage.Dispose();
                    WriteLog(string.Format("이미지가 존재하지 않습니다. 파일명 :  {0} ---------------", filename));
                    return true;
                }
            }

            catch (Exception ex)
            {
                WriteLog(string.Format("---------------[Error] 이미지변환 Error 내용 : {0}---------------", ex.ToString()));
                return false;
                throw ex;
            }
        }


        /// <summary>
        /// 파일사이즈
        /// </summary>
        /// <param name="lBytes"></param>
        /// <returns></returns>
        public static string HumanReadableFileSize(long lBytes)
        {
            var sb = new StringBuilder();
            string strUnits = "Bytes";
            float fAdjusted = 0.0F;

            if (lBytes > 1024)
            {
                if (lBytes < 1024 * 1024)
                {
                    strUnits = "KB";
                    fAdjusted = Convert.ToSingle(lBytes) / 1024;
                }
                else
                {
                    strUnits = "MB";
                    fAdjusted = Convert.ToSingle(lBytes) / 1048576;
                }
                sb.AppendFormat("{0:0.0} {1}", fAdjusted, strUnits);
            }
            else
            {
                fAdjusted = Convert.ToSingle(lBytes);
                sb.AppendFormat("{0:0} {1}", fAdjusted, strUnits);
            }

            return sb.ToString();
        }


        public  static int width (int image_width)
        {
            int width = 1024;

            if(image_width.Equals(width))
            {
                return 1024;
            }
            int image_width_result = image_width - 1024;
            return image_width_result;
        }

        public static int height(int image_height)
        {
            int height = 480;
            if (image_height.Equals(height))
            {
                return 480;
            }
            
            int image_height_result = image_height - height;  //1024-1204 
            return image_height_result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="targetStream"></param>
        /// <param name="maxWidth"></param>
        /// <param name="maxHeight"></param>
        /// <param name="preserverAspectRatio"></param>
        /// <param name="quality"></param>
        /// <returns></returns>
        public static string OptimizeImage(string filename, string filedir , string sourceFile, FileStream targetStream, int maxWidth, int maxHeight, bool preserverAspectRatio)
        {
            string ret = string.Empty;
            string new_file = string.Empty;
            using (System.Drawing.Image sourceImage = (targetStream == null) ? System.Drawing.Image.FromFile(sourceFile) : System.Drawing.Image.FromStream(targetStream))
            {
                // If 0 is passed in any of the max sizes it means that that size must be ignored,
                // so the original image size is used.
                maxWidth = maxWidth == 0 ? sourceImage.Width : maxWidth;
                maxHeight = maxHeight == 0 ? sourceImage.Height : maxHeight;

                if (!Directory.Exists(Path.GetDirectoryName(sourceFile)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));
                }

                Size oSize;
                oSize = new Size(maxWidth, maxHeight);

                using (System.Drawing.Image oResampled = new Bitmap(oSize.Width, oSize.Height, sourceImage.PixelFormat))
                {
                    using (Graphics oGraphics = Graphics.FromImage(oResampled))
                    {
                        Rectangle oRectangle;

                        oRectangle = new Rectangle(0, 0, oSize.Width, oSize.Height);

                        // Place a white background (for transparent images).
                        oGraphics.FillRectangle(new SolidBrush(Color.White), oRectangle);

                        // Draws over the oResampled image the resampled Image
                        oGraphics.DrawImage(sourceImage, oRectangle);

                        sourceImage.Dispose();

                        new_file = string.Format("{0}{1}_0.jpg", filedir, filename); //와이드 이미지 _0
                        oResampled.Save(new_file);

                        ret = new_file;

                        //String extension = System.IO.Path.GetExtension(sourceFile).ToLower();

                        //if (extension.ToLower() == ".jpg" || extension.ToLower() == ".jpeg")
                        //{
                        //    new_file = string.Format("{0}{1}_0.jpg", filedir, filename); //와이드 이미지 _0
                        //    oResampled.Save(new_file);

                        //    ret = new_file;
                        //}
                        //else
                        //{
                        //    switch (extension.ToLower())
                        //    {
                        //        case ".gif":
                        //            new_file = string.Format("{0}{1}_0.gif", filedir, filename);
                        //            oResampled.Save(sourceFile, System.Drawing.Imaging.ImageFormat.Gif);
                        //            break;
                        //        case ".png":
                        //            new_file = string.Format("{0}{1}_0.png", filedir, filename);
                        //            oResampled.Save(sourceFile, System.Drawing.Imaging.ImageFormat.Png);
                        //            break;

                        //        case ".bmp":
                        //            new_file = string.Format("{0}{1}_0.bmp", filedir, filename);
                        //            oResampled.Save(sourceFile, System.Drawing.Imaging.ImageFormat.Bmp);
                        //            break;
                        //    }
                        //    ret = new_file;
                        //}
                    }
                }
            }



            //oResampled.Dispose();

            return new_file;
        }

        private static Size GetAspectRatioSize(int maxWidth, int maxHeight, int actualWidth, int actualHeight)
        {
            // Creates the Size object to be returned
            Size oSize = new System.Drawing.Size(maxWidth, maxHeight);

            // Calculates the X and Y resize factors
            //float iFactorX = (float)maxWidth / (float)actualWidth;
            //float iFactorY = (float)maxHeight / (float)actualHeight;

            //// If some dimension have to be scaled
            //if (iFactorX != 1 || iFactorY != 1)
            //{
            //    // Uses the lower Factor to scale the opposite size
            //    if (iFactorX < iFactorY) { oSize.Height = (int)Math.Round((float)actualHeight * iFactorX); }
            //    else if (iFactorX > iFactorY) { oSize.Width = (int)Math.Round((float)actualWidth * iFactorY); }
            //}

            //if (oSize.Height <= 0) oSize.Height = 1;
            //if (oSize.Width <= 0) oSize.Width = 1;

            int nPercentW = ((int)actualWidth / 1024);
            int destHeight = (int)(actualHeight / nPercentW);

            oSize.Width = 1024;
            oSize.Height = destHeight;

            // Returns the Size
            return oSize;
        }

        private static ImageCodecInfo GetJpgCodec()
        {
            ImageCodecInfo[] aCodecs = ImageCodecInfo.GetImageEncoders();
            ImageCodecInfo oCodec = null;

            for (int i = 0; i < aCodecs.Length; i++)
            {
                if (aCodecs[i].MimeType.Equals("image/jpeg"))
                {
                    oCodec = aCodecs[i];
                    break;
                }
            }

            return oCodec;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logMessage"></param>
        public static void WriteLog(String logMessage)
        {
            Console.WriteLine(logMessage);
            //iLog.Error(logMessage);
            if (!Directory.Exists(Path.GetDirectoryName(LogFIlePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFIlePath));
            }

            if (!File.Exists(LogFIlePath)) File.Create(LogFIlePath).Close();
            StreamWriter w = File.AppendText(LogFIlePath);


            w.Write("{0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            w.Write(" : ");
            w.WriteLine("{0}", logMessage);
            w.Flush();

            w.Close();

        }
    }
}

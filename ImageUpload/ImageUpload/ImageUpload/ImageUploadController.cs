using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PIMBll;
using PIMModel;
using PIMInputSystem;
using System.Web;
using System.Configuration;
using System.IO;
using System.Drawing;


namespace ImageUpload
{
    public class ImageUploadController
    {
        private Guid getProductLanguageGuid(ProductBulk prod)
        {
            if (prod._parentlisttypeid == PIMDemo.csVariables.BulkGenericCompany)
                return prod._productLanguageGuid;
            else if (prod._parentlisttypeid == PIMDemo.csVariables.FinishedGoods)
                return prod.product._productlanguageGuid;
            else
                return prod._bulkgeneric_productLanguageGuid;
        }
        protected void uploadImageByUsageTypeID(HttpContext context, int usageTypeID)
        {
            try
            {
                string StrServerName = ConfigurationManager.AppSettings["InternalImageServer"];
                PIMEntities pimcontext = new PIMEntities(); 
                ProductBulk prod = context.Session["ProductBulk"] as ProductBulk;

                string filename = context.Request.Headers["X-File-Name"];
                string sku = getSKU(filename);

                Guid productlanguageGuid = getProductLanguageGuid(prod);

                string tempPath = Path.GetTempPath() + filename;
                Stream inputStream = context.Request.InputStream;

                FileStream fileStream = new FileStream(tempPath, FileMode.OpenOrCreate);
                inputStream.CopyTo(fileStream);
                fileStream.Close();

                var queryBrand = (from b in pimcontext.Brands
                                  where b.BrandID == prod._brandid
                                  select b);

                Brand brand = queryBrand.FirstOrDefault();

                var queryCompany = (from c in pimcontext.Companies
                                    where c.CompanyID == prod._companyid
                                    select c);

                Company company = queryCompany.FirstOrDefault();

                string absoluteImagePath = string.Empty;
                string virtualImagePath = string.Empty;
                string description = string.Empty;

                switch (prod._parentlisttypeid)
                {
                    case 29:
                        absoluteImagePath = "WebsiteImages/BulkGeneric/ProductImages";
                        virtualImagePath = "BulkGeneric/ProductImages";
                        break;
                    case 30:
                        absoluteImagePath = brand.BulkGenericWebAbsoluteImagePath;
                        virtualImagePath = brand.BulkGenericWebVirtualImagePath;
                        break;
                    case 31:
                        absoluteImagePath = brand.AbsoluteWebImagePath;
                        virtualImagePath = brand.VirtualImagePath;
                        break;
                    default:
                        break;
                }
                switch (usageTypeID)
                {
                    case 24:
                        description = "Preview Image";
                        break;
                    case 25:
                        description = "Feature Image";
                        break;
                    case 26:
                        description = "Label Image";
                        break;
                    case 27:
                        description = "High-res Image";
                        break;
                    case 197:
                        description = "Thumbnail Image";
                        break;
                    case 384:
                        description = "Mockup Image";
                        break;
                    case 190:
                        description = "Product Profile Banner";
                        absoluteImagePath = company.CategoriesImagePath;
                        virtualImagePath = company.CategoriesImageWebPath;
                        break;
                    case 191:
                        description = "Research Information Sheet Banner";
                        absoluteImagePath = company.CategoriesImagePath;
                        virtualImagePath = company.CategoriesImageWebPath;
                        break;
                    case 397:
                        description = "Brand Header Logo";
                        absoluteImagePath = brand.BrandReportHeaderImagePath;
                        virtualImagePath = string.Empty;
                        break;
                    default:
                        break;
                }

                var queryFile = (from f in pimcontext.FileAttachments
                                 where f.ProductLanguageGUID == productlanguageGuid &&
                                    f.UsageTypeID == usageTypeID &&
                                    f.ParentListTypeID == prod._parentlisttypeid &&
                                    f.AttachmentTypeID == PIMDemo.csVariables.AttachmentType.Image
                                 select f);

                FileAttachment fa;

                if (queryFile.Count() > 0)
                {
                    fa = queryFile.FirstOrDefault();

                    /* delete original bulk level product image from ftp image server */
                    if (File.Exists(StrServerName + "\\" + fa.FileName))
                        File.Delete(StrServerName + "\\" + fa.FileName);

                    fa.FileName = (Path.HasExtension(absoluteImagePath)) ? Path.GetDirectoryName(absoluteImagePath) + "\\" + filename : absoluteImagePath + "\\" + filename;
                    fa.WebPath = (!string.IsNullOrEmpty(virtualImagePath)) ? virtualImagePath + "/" + filename : string.Empty;
                    fa.DateModified = DateTime.Now;
                    fa.DisplayValue = description;
                    fa.Description = description;

                    pimcontext.SaveChanges();

                }
                else
                {
                    fa = new FileAttachment()
                    {
                        FileName = (Path.HasExtension(absoluteImagePath)) ? Path.GetDirectoryName(absoluteImagePath) + "\\" + filename : absoluteImagePath + "\\" + filename,
                        WebPath = (!string.IsNullOrEmpty(virtualImagePath)) ? "/" + virtualImagePath + "/" + filename : string.Empty,
                        UsageTypeID = usageTypeID,
                        ParentListTypeID = prod._parentlisttypeid,
                        ProductLanguageGUID = productlanguageGuid,
                        AttachmentTypeID = PIMDemo.csVariables.AttachmentType.Image,
                        AccessLevel = PIMDemo.csVariables.AccessLevel.Visible,
                        DisplayValue = description,
                        Description = description,
                        DateCreated = DateTime.Now,
                        DateModified = DateTime.Now,
                        LanguageID = prod._languageid
                    };
                    pimcontext.AddToFileAttachments(fa);
                    pimcontext.SaveChanges();
                }


                File.Copy(tempPath, StrServerName + "\\" + fa.FileName, true);

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                if (usageTypeID == PIMDemo.csVariables.UsageTypes.ProductProfileBanner ||
                    usageTypeID == PIMDemo.csVariables.UsageTypes.ResearchInfoBanner ||
                    usageTypeID == PIMDemo.csVariables.UsageTypes.ProductProfileBrandHeaderLogo)
                {
                    ReportsHeader report = (from rh in pimcontext.ReportsHeaders
                                            where rh.ProductLanguageGUID == productlanguageGuid && rh.ParentListTypeID == prod._parentlisttypeid
                                            select rh).FirstOrDefault();

                    if (report == null)
                    {
                        ReportsHeader newReport = new ReportsHeader();
                        newReport.Description = prod._primaryname;
                        newReport.ParentListTypeID = prod._parentlisttypeid;
                        newReport.ProductLanguageGUID = productlanguageGuid;
                        if (usageTypeID == PIMDemo.csVariables.UsageTypes.ResearchInfoBanner)
                        {
                            newReport.FileAttachmentID = fa.FileAttachmentID;
                            newReport.ReportTypeID = PIMDemo.csVariables.ReportType.ResearchInformation;
                        }
                        else if (usageTypeID == PIMDemo.csVariables.UsageTypes.ProductProfileBrandHeaderLogo)
                        {
                            newReport.HeaderLogoFileAttachmentID = fa.FileAttachmentID;
                            newReport.ReportTypeID = PIMDemo.csVariables.ReportType.ProductProfile;
                        }
                        else
                        {
                            newReport.FileAttachmentID = fa.FileAttachmentID;
                            newReport.ReportTypeID = PIMDemo.csVariables.ReportType.ProductProfile;
                        }
                        newReport.DateCreated = DateTime.Now;
                        newReport.DateModified = DateTime.Now;
                        pimcontext.AddToReportsHeaders(newReport);

                    }
                    else
                    {
                        if (usageTypeID == PIMDemo.csVariables.UsageTypes.ProductProfileBrandHeaderLogo)
                            report.HeaderLogoFileAttachmentID = fa.FileAttachmentID;
                        else
                            report.FileAttachmentID = fa.FileAttachmentID;
                        report.DateModified = DateTime.Now;
                    }
                    pimcontext.SaveChanges();
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex.InnerException);

            }
        }
        protected void UploadProductBrief(HttpContext context, int usageTypeID)
        {
            try
            {
                string StrServerName = ConfigurationManager.AppSettings["InternalImageServer"];
                PIMEntities pimcontext = new PIMEntities();
                ProductBulk prod = context.Session["ProductBulk"] as ProductBulk; //HttpContext.Current.Session["ProductBulk"] as ProductBulk;

                string filename = context.Request.Headers["X-File-Name"];
                string sku = getSKU(filename);

                Guid productlanguageGuid = getProductLanguageGuid(prod);

                string tempPath = Path.GetTempPath() + filename;
                Stream inputStream = context.Request.InputStream;

                FileStream fileStream = new FileStream(tempPath, FileMode.OpenOrCreate);
                inputStream.CopyTo(fileStream);
                fileStream.Close();

                var queryBrand = (from b in pimcontext.Brands
                                  where b.BrandID == prod._brandid
                                  select b);

                Brand brand = queryBrand.FirstOrDefault();
                string absoluteImagePath = string.Empty;
                string virtualImagePath = string.Empty;

                switch (prod._parentlisttypeid)
                {
                    case 29:
                        absoluteImagePath = "WebsiteImages/BulkGeneric/ProductImages";
                        virtualImagePath = "BulkGeneric/ProductImages";
                        break;
                    case 30:
                        absoluteImagePath = brand.BulkGenericWebAbsoluteImagePath;
                        virtualImagePath = brand.BulkGenericWebVirtualImagePath;
                        break;
                    case 31:
                        absoluteImagePath = brand.AbsoluteWebImagePath;
                        virtualImagePath = brand.VirtualImagePath;
                        break;
                    default:
                        break;
                }

                var queryFile = (from f in pimcontext.FileAttachments
                                 where f.ProductLanguageGUID == productlanguageGuid &&
                                    f.UsageTypeID == usageTypeID &&
                                    f.ParentListTypeID == prod._parentlisttypeid &&
                                    f.AttachmentTypeID == PIMDemo.csVariables.AttachmentType.Image
                                 select f);

                if (queryFile.Count() > 0)
                {
                    FileAttachment fa = queryFile.FirstOrDefault();

                    /* delete original bulk level product image from ftp image server */
                    if (File.Exists(StrServerName + "\\" + fa.FileName))
                        File.Delete(StrServerName + "\\" + fa.FileName);

                    fa.FileName = absoluteImagePath + "\\" + filename;
                    fa.WebPath = virtualImagePath + "/" + filename;
                    fa.DateModified = DateTime.Now;

                }
                else
                {
                    FileAttachment fa = new FileAttachment()
                    {
                        FileName = absoluteImagePath + "\\" + filename,
                        WebPath = "/" + virtualImagePath + "/" + filename,
                        UsageTypeID = usageTypeID,
                        ParentListTypeID = prod._parentlisttypeid,
                        ProductLanguageGUID = productlanguageGuid,
                        AttachmentTypeID = PIMDemo.csVariables.AttachmentType.Image,
                        AccessLevel = PIMDemo.csVariables.AccessLevel.Visible,
                        DisplayValue = "Product Brief Image",
                        Description = "Product Brief Image",
                        DateCreated = DateTime.Now,
                        DateModified = DateTime.Now,
                        LanguageID = prod._languageid
                    };
                    pimcontext.AddToFileAttachments(fa);
                }
                pimcontext.SaveChanges();

                File.Copy(tempPath, StrServerName + "\\" + absoluteImagePath + "\\" + filename, true);

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex.InnerException);
            }

        }
        protected void UploadMultipleImage(HttpContext context)
        {
            PIMEntities pimcontext = new PIMEntities();

            ProductBulk prod = HttpContext.Current.Session["ProductBulk"] as ProductBulk;
            string filename = context.Request.Headers["X-File-Name"];
            string sku = getSKU(filename);
            int companyid = Convert.ToInt32(context.Request.Params["companyid"]);
            int brandid = Convert.ToInt32(context.Request.Params["brandid"]);
            int languageid = Convert.ToInt32(context.Request.Params["languageid"]);
            int accessid = Convert.ToInt32(context.Request.Params["accessid"]);

            var queryBrand = (from b in pimcontext.Brands where b.BrandID == brandid select b).FirstOrDefault();

            var ProductGuidList = (from fp in pimcontext.Products
                                   join fpl in pimcontext.ProductLanguages on fp.ProductID equals fpl.ProductID
                                   join bl in pimcontext.BulkFinishedGoodsLinks on fp.ProductID equals bl.ProductID
                                   join bgp in pimcontext.BulkGenericCompanyProducts on bl.BulkGenericCompanyProductID equals bgp.BulkGenericCompanyProductID
                                   join bgl in pimcontext.BulkGenericCompanyLanguages on bgp.BulkGenericCompanyProductID equals bgl.BulkGenericCompanyProductID
                                   join bp in pimcontext.BulkGenericProducts on bgp.BulkGenericProductID equals bp.BulkGenericProductID
                                   where bgl.LanguageId == languageid && fpl.LanguageID == languageid && bgp.CompanyID == companyid && bgp.BrandID == brandid && fp.ProductCodeCDN == sku
                                   select new
                                   {
                                       bulkcode = bp.BulkCode,
                                       fpguid = fpl.ProductLanguageGUID,
                                       bulkguid = bgl.ProductLanguageGUID
                                   }).FirstOrDefault();

            if (ProductGuidList == null)
            {
                ShowMessage("Invalid file. Please make sure you select the company, brand, and language. Also make sure the SKU is the first 4 characters of the filename.");
            }
            else
            {
                try
                {
                    string tempPath = Path.GetTempPath() + filename;
                    Stream inputStream = context.Request.InputStream;

                    FileStream fileStream = new FileStream(tempPath, FileMode.OpenOrCreate);
                    inputStream.CopyTo(fileStream);
                    fileStream.Close();

                    System.Drawing.Image source = System.Drawing.Image.FromFile(tempPath);

                    Bitmap HighresImage = ImageResizing(ImageDimensions.highres, ImageDimensions.highres, source);
                    Bitmap featureImage = ImageResizing(ImageDimensions.feature, ImageDimensions.feature, source);
                    Bitmap previewImage = ImageResizing(ImageDimensions.preview, ImageDimensions.preview, source);
                    Bitmap thumbnailImage = ImageResizing(ImageDimensions.thumbnail, ImageDimensions.thumbnail, source);

                    source.Dispose();

                    string fp_highres_filename = fileRename(filename, PIMDemo.csVariables.UsageTypes.HiRes, queryBrand);
                    if (!string.IsNullOrEmpty(fp_highres_filename))
                    {
                        //if high res image has a different name than the source, delete source image
                        if (Path.GetTempPath() + fp_highres_filename != tempPath)
                        {
                            if (File.Exists(tempPath)) File.Delete(tempPath);
                            HighresImage.Save(Path.GetTempPath() + fp_highres_filename);
                        }
                        UpdateImage(Path.GetTempPath() + fp_highres_filename, languageid, queryBrand, ProductGuidList.fpguid, PIMDemo.csVariables.UsageTypes.HiRes, PIMDemo.csVariables.FinishedGoods, PIMDemo.csVariables.AttachmentType.Image, accessid);

                    }

                    string fp_feature_filename = fileRename(filename, PIMDemo.csVariables.UsageTypes.Feature, queryBrand);
                    if (!string.IsNullOrEmpty(fp_feature_filename))
                    {
                        featureImage.Save(Path.GetTempPath() + fp_feature_filename);
                        UpdateImage(Path.GetTempPath() + fp_feature_filename, languageid, queryBrand, ProductGuidList.fpguid, PIMDemo.csVariables.UsageTypes.Feature, PIMDemo.csVariables.FinishedGoods, PIMDemo.csVariables.AttachmentType.Image, accessid);

                    }

                    string fp_preview_filename = fileRename(filename, PIMDemo.csVariables.UsageTypes.Preview, queryBrand);
                    if (!string.IsNullOrEmpty(fp_preview_filename))
                    {
                        previewImage.Save(Path.GetTempPath() + fp_preview_filename);
                        UpdateImage(Path.GetTempPath() + fp_preview_filename, languageid, queryBrand, ProductGuidList.fpguid, PIMDemo.csVariables.UsageTypes.Preview, PIMDemo.csVariables.FinishedGoods, PIMDemo.csVariables.AttachmentType.Image, accessid);

                    }

                    string fp_thumbnail_filename = fileRename(filename, PIMDemo.csVariables.UsageTypes.Thumbnail, queryBrand);
                    if (!string.IsNullOrEmpty(fp_thumbnail_filename))
                    {
                        thumbnailImage.Save(Path.GetTempPath() + fp_thumbnail_filename);
                        UpdateImage(Path.GetTempPath() + fp_thumbnail_filename, languageid, queryBrand, ProductGuidList.fpguid, PIMDemo.csVariables.UsageTypes.Thumbnail, PIMDemo.csVariables.FinishedGoods, PIMDemo.csVariables.AttachmentType.Image, accessid);

                    }

                    if (!string.IsNullOrEmpty(context.Request.Headers["SetBulkImage"]) && context.Request.Headers["SetBulkImage"] == "1")
                    {
                        //Save current image as the bulk image.
                        string newFileName = ProductGuidList.bulkcode + filename.Substring(filename.IndexOf("_"));

                        string bulk_feature_filename = fileRename(newFileName, PIMDemo.csVariables.UsageTypes.Feature, queryBrand);
                        if (!string.IsNullOrEmpty(bulk_feature_filename))
                        {
                            featureImage.Save(Path.GetTempPath() + bulk_feature_filename);
                            UpdateImage(Path.GetTempPath() + bulk_feature_filename, languageid, queryBrand, ProductGuidList.bulkguid, PIMDemo.csVariables.UsageTypes.Feature, PIMDemo.csVariables.BulkGenericCompany, PIMDemo.csVariables.AttachmentType.Image, accessid);

                        }
                        string bulk_preview_filename = fileRename(newFileName, PIMDemo.csVariables.UsageTypes.Preview, queryBrand);
                        if (!string.IsNullOrEmpty(bulk_preview_filename))
                        {
                            previewImage.Save(Path.GetTempPath() + bulk_preview_filename);
                            UpdateImage(Path.GetTempPath() + bulk_preview_filename, languageid, queryBrand, ProductGuidList.bulkguid, PIMDemo.csVariables.UsageTypes.Preview, PIMDemo.csVariables.BulkGenericCompany, PIMDemo.csVariables.AttachmentType.Image, accessid);
                        }
                        string bulk_thumbnail_filename = fileRename(newFileName, PIMDemo.csVariables.UsageTypes.Thumbnail, queryBrand);
                        if (!string.IsNullOrEmpty(bulk_thumbnail_filename))
                        {
                            thumbnailImage.Save(Path.GetTempPath() + bulk_thumbnail_filename);
                            UpdateImage(Path.GetTempPath() + bulk_thumbnail_filename, languageid, queryBrand, ProductGuidList.bulkguid, PIMDemo.csVariables.UsageTypes.Thumbnail, PIMDemo.csVariables.BulkGenericCompany, PIMDemo.csVariables.AttachmentType.Image, accessid);
                        }

                        //dispose of the Bitmap objects
                        HighresImage.Dispose();
                        featureImage.Dispose();
                        previewImage.Dispose();
                        thumbnailImage.Dispose();

                        //delete bulk images in temp folder
                        if (File.Exists(Path.GetTempPath() + bulk_feature_filename))
                            File.Delete(Path.GetTempPath() + bulk_feature_filename);
                        if (File.Exists(Path.GetTempPath() + bulk_preview_filename))
                            File.Delete(Path.GetTempPath() + bulk_preview_filename);
                        if (File.Exists(Path.GetTempPath() + bulk_thumbnail_filename))
                            File.Delete(Path.GetTempPath() + bulk_thumbnail_filename);
                    }
                    else
                    {
                        //dispose of the Bitmap objects
                        HighresImage.Dispose();
                        featureImage.Dispose();
                        previewImage.Dispose();
                        thumbnailImage.Dispose();
                    }

                    //delete finished goods images in temp folder
                    if (File.Exists(Path.GetTempPath() + fp_highres_filename))
                        File.Delete(Path.GetTempPath() + fp_highres_filename);
                    if (File.Exists(Path.GetTempPath() + fp_feature_filename))
                        File.Delete(Path.GetTempPath() + fp_feature_filename);
                    if (File.Exists(Path.GetTempPath() + fp_preview_filename))
                        File.Delete(Path.GetTempPath() + fp_preview_filename);
                    if (File.Exists(Path.GetTempPath() + fp_thumbnail_filename))
                        File.Delete(Path.GetTempPath() + fp_thumbnail_filename);

                    ShowMessage("Upload Successful!");
                }
                catch (Exception ex)
                {
                    ShowMessage(ex.Message + " : " + ex.StackTrace);
                }
            }
        }
        protected Bitmap ImageResizing(int canvasWidth, int canvasHeight, System.Drawing.Image source)
        {
            double ratioX = (double)canvasWidth / (double)source.Width;
            double ratioY = (double)canvasHeight / (double)source.Height;

            double ratio = ratioX < ratioY ? ratioX : ratioY;

            int newWidth = Convert.ToInt32(source.Width * ratio);
            int newHeight = Convert.ToInt32(source.Height * ratio);

            Bitmap newImage = new Bitmap(newWidth, newHeight);
            using (Graphics gr = Graphics.FromImage(newImage))
            {
                gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                gr.DrawImage(source, new Rectangle(0, 0, newWidth, newHeight));
            }
            return newImage;
        }
        private string fileRename(string filename, int usagetypeid, Brand brand)
        {
            string sku = getSKU(filename);
            int companyid = Convert.ToInt32(HttpContext.Current.Request.Params["companyid"]);
            int brandid = Convert.ToInt32(HttpContext.Current.Request.Params["brandid"]);
            int languageid = Convert.ToInt32(HttpContext.Current.Request.Params["languageid"]);

            StringBuilder result = new StringBuilder(sku);
            result.AppendFormat("_{0}_", brand.BrandPrefix);

            if (languageid == 1)
                result.Append("CE");
            else if (languageid == 2)
                result.Append("USAE");
            else if (languageid == 3)
                result.Append("CF");
            else
            {
                result.Clear();
                return result.ToString();
            }

            if (usagetypeid == 197)
                result.Append("T");
            else if (usagetypeid == 24)
                result.Append("P");
            else if (usagetypeid == 25)
                result.Append("F");
            else if (usagetypeid == 27)
                result.Append("HR");
            else
            {
                result.Clear();
                return result.ToString();
            }

            result.Append(Path.GetExtension(filename));
            return result.ToString();

        }
        private void UpdateImage(string tempPath, int languageid, Brand brand, Guid prodGuid, int usagetypeid, int parentlisttypeid, int attachmenttypeid, int accessid)
        {
            string strServerName = ConfigurationManager.AppSettings["InternalImageServer"];

            PIMEntities pimcontext = new PIMEntities();

            string filename = Path.GetFileName(tempPath);

            string absoluteImagePath = string.Empty;
            string virtualImagePath = string.Empty;

            if (parentlisttypeid == PIMDemo.csVariables.BulkGenericCompany)
            {
                absoluteImagePath = brand.BulkGenericWebAbsoluteImagePath + "\\" + filename;
                virtualImagePath = "/" + brand.BulkGenericWebVirtualImagePath + "/" + filename;
            }
            else if (parentlisttypeid == PIMDemo.csVariables.FinishedGoods)
            {
                absoluteImagePath = brand.AbsoluteWebImagePath + "\\" + filename;
                virtualImagePath = "/" + brand.VirtualImagePath + "/" + filename;
            }
            else
            {
                absoluteImagePath = "WebsiteImages/BulkGeneric/ProductImages/" + filename;
                virtualImagePath = "/BulkGeneric/ProductImages/" + filename;
            }

            var queryImages = (from f in pimcontext.FileAttachments
                               where f.ProductLanguageGUID == prodGuid
                               && f.UsageTypeID == usagetypeid
                               && f.ParentListTypeID == parentlisttypeid
                               && f.AttachmentTypeID == attachmenttypeid
                               select f);

            FTP ftp = new FTP();
            if (queryImages.Count() > 0)
            {
                FileAttachment fa = queryImages.FirstOrDefault();

                if (File.Exists(strServerName + "\\" + fa.FileName))
                {
                    File.Delete(strServerName + "\\" + fa.FileName);
                }

                ftp.RemoteFile = fa.WebPath;

                if (ftp.FileExists())
                {
                    ftp.DeleteFile();
                }

                fa.FileName = absoluteImagePath;
                fa.WebPath = virtualImagePath;
                fa.DateModified = DateTime.Now;
                fa.AccessLevel = accessid;

                switch (usagetypeid)
                {
                    case 197:
                        fa.DisplayValue = "Thumbnail Image";
                        fa.Description = "Thumbnail Image";
                        break;
                    case 24:
                        fa.DisplayValue = "Preview Image";
                        fa.Description = "Preview Image";
                        break;
                    case 25:
                        fa.DisplayValue = "Feature Image";
                        fa.Description = "Feature Image";
                        break;
                    case 27:
                        fa.DisplayValue = "High-res Image";
                        fa.Description = "High-res Image";
                        break;
                    default:
                        fa.DisplayValue = string.Empty;
                        fa.Description = string.Empty;
                        break;
                }

            }
            else
            {
                FileAttachment fa = new FileAttachment()
                {
                    FileName = absoluteImagePath,
                    WebPath = virtualImagePath,
                    UsageTypeID = usagetypeid,
                    ParentListTypeID = parentlisttypeid,
                    ProductLanguageGUID = prodGuid,
                    AttachmentTypeID = attachmenttypeid,
                    AccessLevel = accessid,
                    DisplayValue = "Product Image",
                    Description = "Product Image",
                    DateCreated = DateTime.Now,
                    DateModified = DateTime.Now,
                    LanguageID = languageid,
                };
                pimcontext.AddToFileAttachments(fa);
            }
            pimcontext.SaveChanges();

            /* Copy files to ftp image server */
            ftp.LocalFile = tempPath;
            ftp.RemoteFile = virtualImagePath;
            ftp.PutFiles();

            /* Copy Image into \\mann server */
            File.Copy(tempPath, strServerName + "\\" + absoluteImagePath, true);

        }
        protected string getSKU(string filename)
        {
            if (filename.IndexOf("-") != -1)
                return filename.Substring(0, filename.IndexOf("-"));
            else if (filename.IndexOf("_") != -1)
                return filename.Substring(0, filename.IndexOf("_"));
            else if (filename.IndexOf(".") != -1)
                return filename.Substring(0, filename.IndexOf("."));
            else
                return string.Empty;


        }
        public void ShowMessage(string msg)
        {
            HttpContext.Current.Response.Clear();
            HttpContext.Current.Response.ContentType = "text/plain";
            HttpContext.Current.Response.Write(msg);
        }

    }
}

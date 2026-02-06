using CrystalGroupHome.SharedRCL.Data.Parts;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;

namespace CrystalGroupHome.SharedRCL.Helpers
{
    public static class PDFHelpers
    {
        /// <summary>
        /// Fills a fillable PDF form with values from a dictionary and saves to a new file.
        /// </summary>
        public static void FillPdfForm00013(
            string sourcePdfPath,
            string outputPdfPath,
            Dictionary<string, string> formData,
            string? title = null,
            bool removeSecondPage = false,
            bool isConfirmOfImp = false,
            PartDTO_Base? part = null)
        {
            using var reader = new PdfReader(sourcePdfPath);
            using var writer = new PdfWriter(outputPdfPath);
            using var pdfDoc = new PdfDocument(reader, writer);
            var form = PdfAcroForm.GetAcroForm(pdfDoc, true);
            var fields = form.GetAllFormFields();

            foreach (var (key, value) in formData)
            {
                if (fields.ContainsKey(key))
                {
                    fields[key].SetValue(value ?? string.Empty);
                    fields[key].SetReadOnly(true);
                }
            }

            if (isConfirmOfImp && part != null)
            {
                fields["REV"].SetValue("Rev " + part.RevisionNum);
                fields["PRICE_AFFECTS_LABEL"].GetFirstFormAnnotation().SetVisibility(PdfFormAnnotation.HIDDEN);

                if (fields.ContainsKey("PRICE_AFFECTS"))
                {
                    var priceAffectsField = fields["PRICE_AFFECTS"];
                    priceAffectsField.GetFirstFormAnnotation().SetVisibility(PdfFormAnnotation.HIDDEN);
                    var annotations = priceAffectsField.GetChildFormAnnotations();
                    foreach (var ann in annotations)
                    {
                        ann.SetVisibility(PdfFormAnnotation.HIDDEN);
                    }
                }
            }
            else
            {
                fields["REV"].SetValue("Rev TBD");
                fields["EFFECTIVE_DATE"].SetValue("TBD");
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                var info = pdfDoc.GetDocumentInfo();
                info.SetTitle(title);
            }

            if (removeSecondPage && pdfDoc.GetNumberOfPages() >= 2)
            {
                pdfDoc.RemovePage(2);
            }
        }

        /// <summary>
        /// Returns all field names from a fillable PDF.
        /// </summary>
        public static List<string> GetPdfFieldNames(string pdfPath)
        {
            using var reader = new PdfReader(pdfPath);
            using var pdfDoc = new PdfDocument(reader);
            var form = PdfAcroForm.GetAcroForm(pdfDoc, false);
            var fields = form.GetAllFormFields();

            return new List<string>(fields.Keys);
        }

        /// <summary>
        /// Dumps all field names and current values (for inspection/debug).
        /// </summary>
        public static Dictionary<string, string> GetPdfFieldValues(string pdfPath)
        {
            var result = new Dictionary<string, string>();

            using var reader = new PdfReader(pdfPath);
            using var pdfDoc = new PdfDocument(reader);
            var form = PdfAcroForm.GetAcroForm(pdfDoc, false);
            var fields = form.GetAllFormFields();

            foreach (var entry in fields)
            {
                var name = entry.Key;
                var value = entry.Value.GetValueAsString();
                result[name] = value;
            }

            return result;
        }
    }
}

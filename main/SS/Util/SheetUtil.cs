/* ====================================================================
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for Additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
==================================================================== */

namespace NPOI.SS.Util
{
    using System;

    using NPOI.SS.UserModel;
    using System.Drawing;
    using NPOI.HSSF.UserModel;
    using System.Windows.Forms;
    using System.Collections;
    using System.Globalization;

    /**
     * Helper methods for when working with Usermodel sheets
     *
     * @author Yegor Kozlov
     */
    public class SheetUtil
    {

        // /**
        // * Excel measures columns in units of 1/256th of a character width
        // * but the docs say nothing about what particular character is used.
        // * '0' looks to be a good choice.
        // */
        private static char defaultChar = '0';

        // /**
        // * This is the multiple that the font height is scaled by when determining the
        // * boundary of rotated text.
        // */
        //private static double fontHeightMultiple = 2.0;

        /**
         *  Dummy formula Evaluator that does nothing.
         *  YK: The only reason of having this class is that
         *  {@link NPOI.SS.UserModel.DataFormatter#formatCellValue(NPOI.SS.UserModel.Cell)}
         *  returns formula string for formula cells. Dummy Evaluator Makes it to format the cached formula result.
         *
         *  See Bugzilla #50021 
         */
        private IFormulaEvaluator dummyEvaluator = new DummyEvaluator();
        public class DummyEvaluator : IFormulaEvaluator
        {
            public void ClearAllCachedResultValues() { }
            public void NotifySetFormula(ICell cell) { }
            public void NotifyDeleteCell(ICell cell) { }
            public void NotifyUpdateCell(ICell cell) { }
            public CellValue Evaluate(ICell cell) { return null; }
            public ICell EvaluateInCell(ICell cell) { return null; }
            public void EvaluateAll() { }

            public CellType EvaluateFormulaCell(ICell cell)
            {
                return cell.CachedFormulaResultType;
            }

        };
        public static IRow CopyRow(ISheet sheet, int sourceRowIndex, int targetRowIndex)
        {
            if (sourceRowIndex == targetRowIndex)
                throw new ArgumentException("sourceIndex and targetIndex cannot be same");
            // Get the source / new row
            IRow newRow = sheet.GetRow(targetRowIndex);
            IRow sourceRow = sheet.GetRow(sourceRowIndex);

            // If the row exist in destination, push down all rows by 1 else create a new row
            if (newRow != null)
            {
                sheet.ShiftRows(targetRowIndex, sheet.LastRowNum, 1);
            }
            else
            {
                newRow = sheet.CreateRow(targetRowIndex);
            }

            // Loop through source columns to add to new row
            for (int i = sourceRow.FirstCellNum; i < sourceRow.LastCellNum; i++)
            {
                // Grab a copy of the old/new cell
                ICell oldCell = sourceRow.GetCell(i);

                // If the old cell is null jump to next cell
                if (oldCell == null)
                {
                    continue;
                }
                ICell newCell = newRow.CreateCell(i);

                if (oldCell.CellStyle != null)
                {
                    // apply style from old cell to new cell 
                    newCell.CellStyle = oldCell.CellStyle;
                }

                // If there is a cell comment, copy
                if (oldCell.CellComment != null)
                {
                    newCell.CellComment = oldCell.CellComment;
                }

                // If there is a cell hyperlink, copy
                if (oldCell.Hyperlink != null)
                {
                    newCell.Hyperlink = oldCell.Hyperlink;
                }

                // Set the cell data type
                newCell.SetCellType(oldCell.CellType);

                // Set the cell data value
                switch (oldCell.CellType)
                {
                    case CellType.BLANK:
                        newCell.SetCellValue(oldCell.StringCellValue);
                        break;
                    case CellType.BOOLEAN:
                        newCell.SetCellValue(oldCell.BooleanCellValue);
                        break;
                    case CellType.ERROR:
                        newCell.SetCellErrorValue(oldCell.ErrorCellValue);
                        break;
                    case CellType.FORMULA:
                        newCell.SetCellFormula(oldCell.CellFormula);
                        break;
                    case CellType.NUMERIC:
                        newCell.SetCellValue(oldCell.NumericCellValue);
                        break;
                    case CellType.STRING:
                        newCell.SetCellValue(oldCell.RichStringCellValue);
                        break;
                }
            }

            // If there are are any merged regions in the source row, copy to new row
            for (int i = 0; i < sheet.NumMergedRegions; i++)
            {
                CellRangeAddress cellRangeAddress = sheet.GetMergedRegion(i);
                if (cellRangeAddress.FirstRow == sourceRow.RowNum)
                {
                    CellRangeAddress newCellRangeAddress = new CellRangeAddress(newRow.RowNum,
                            (newRow.RowNum +
                                    (cellRangeAddress.LastRow - cellRangeAddress.FirstRow
                                            )),
                            cellRangeAddress.FirstColumn,
                            cellRangeAddress.LastColumn);
                    sheet.AddMergedRegion(newCellRangeAddress);
                }
            }
            return newRow;
        }
        // /**
        // * Drawing context to measure text
        // */
        //private static FontRenderContext fontRenderContext = new FontRenderContext(null, true, true);

        /**
         * Compute width of a column and return the result
         *
         * @param sheet the sheet to calculate
         * @param column    0-based index of the column
         * @param useMergedCells    whether to use merged cells
         * @return  the width in pixels
         */
        public static double GetColumnWidth(ISheet sheet, int column, bool useMergedCells)
        {
            //AttributedString str;
            //TextLayout layout;

            IWorkbook wb = sheet.Workbook;
            DataFormatter formatter = new DataFormatter();
            IFont defaultFont = wb.GetFontAt((short)0);

            //str = new AttributedString((defaultChar));
            //copyAttributes(defaultFont, str, 0, 1);
            //layout = new TextLayout(str.Iterator, fontRenderContext);
            //int defaultCharWidth = (int)layout.Advance;
            Font font = IFont2Font(defaultFont);
            int defaultCharWidth = TextRenderer.MeasureText("" + new String(defaultChar, 1), font).Width;
            DummyEvaluator dummyEvaluator = new DummyEvaluator();

            double width = -1;
            using (Bitmap bmp = new Bitmap(2048, 100))
            {
                Graphics g = Graphics.FromImage(bmp);
                //rows:
                bool skipthisrow = false;
                for (IEnumerator it = sheet.GetRowEnumerator(); it.MoveNext(); )
                {
                    IRow row = (IRow)it.Current;
                    ICell cell = row.GetCell(column);

                    if (cell == null)
                    {
                        continue;
                    }

                    int colspan = 1;
                    for (int i = 0; i < sheet.NumMergedRegions; i++)
                    {
                        CellRangeAddress region = sheet.GetMergedRegion(i);
                        if (ContainsCell(region, row.RowNum, column))
                        {
                            if (!useMergedCells)
                            {
                                // If we're not using merged cells, skip this one and Move on to the next.
                                //continue rows;
                                skipthisrow = true;
                            }
                            cell = row.GetCell(region.FirstColumn);
                            colspan = 1 + region.LastColumn - region.FirstColumn;
                        }
                    }
                    if (skipthisrow)
                    {
                        continue;
                    }
                    ICellStyle style = cell.CellStyle;
                    NPOI.SS.UserModel.IFont font1 = wb.GetFontAt(style.FontIndex);

                    CellType cellType = cell.CellType;

                    // for formula cells we compute the cell width for the cached formula result
                    if (cellType == CellType.FORMULA) cellType = cell.CachedFormulaResultType;

                    if (cellType == CellType.STRING)
                    {
                        IRichTextString rt = cell.RichStringCellValue;
                        String[] lines = rt.String.Split("\n".ToCharArray());
                        for (int i = 0; i < lines.Length; i++)
                        {
                            String txt = lines[i] + defaultChar;

                            //str = new AttributedString(txt);
                            //copyAttributes(font, str, 0, txt.Length);
                            font = IFont2Font(font1);
                            if (rt.NumFormattingRuns > 0)
                            {
                                // TODO: support rich text fragments
                            }

                            //layout = new TextLayout(str.Iterator, fontRenderContext);
                            if (style.Rotation != 0)
                            {
                                /*
                                 * Transform the text using a scale so that it's height is increased by a multiple of the leading,
                                 * and then rotate the text before computing the bounds. The scale results in some whitespace around
                                 * the unrotated top and bottom of the text that normally wouldn't be present if unscaled, but
                                 * is Added by the standard Excel autosize.
                                 */
                                double angle = style.Rotation * 2.0 * Math.PI / 360.0;
                                //AffineTransform trans = new AffineTransform();
                                //trans.Concatenate(AffineTransform.GetRotateInstance(style.Rotation*2.0*Math.PI/360.0));
                                //trans.Concatenate(
                                //AffineTransform.GetScaleInstance(1, fontHeightMultiple)
                                //);
                                SizeF sf = g.MeasureString(txt, font);
                                double x1 = Math.Abs(sf.Height * Math.Sin(angle));
                                double x2 = Math.Abs(sf.Width * Math.Cos(angle));
                                double w = Math.Round(x1 + x2, 0, MidpointRounding.ToEven);
                                width = Math.Max(width, (w / colspan / defaultCharWidth) * 2 + cell.CellStyle.Indention);
                                //width = Math.Max(width, ((layout.GetOutline(trans).Bounds.Width / colspan) / defaultCharWidth) + cell.CellStyle.Indention);
                            }
                            else
                            {
                                //width = Math.Max(width, ((layout.Bounds.Width / colspan) / defaultCharWidth) + cell.CellStyle.Indention);
                                double w = Math.Round(g.MeasureString(txt, font).Width, 0, MidpointRounding.ToEven);
                                width = Math.Max(width, (w / colspan / defaultCharWidth) * 2 + cell.CellStyle.Indention);
                            }
                        }
                    }
                    else
                    {
                        String sval = null;
                        if (cellType == CellType.NUMERIC)
                        {
                            // Try to Get it formatted to look the same as excel
                            try
                            {
                                sval = formatter.FormatCellValue(cell, dummyEvaluator);
                            }
                            catch (Exception)
                            {
                                sval = cell.NumericCellValue.ToString("F", CultureInfo.InvariantCulture);
                            }
                        }
                        else if (cellType == CellType.BOOLEAN)
                        {
                            sval = cell.BooleanCellValue.ToString().ToUpper();
                        }
                        if (sval != null)
                        {
                            String txt = sval + defaultChar;
                            //str = new AttributedString(txt);
                            //copyAttributes(font, str, 0, txt.Length);

                            //layout = new TextLayout(str.Iterator, fontRenderContext);
                            if (style.Rotation != 0)
                            {
                                /*
                                 * Transform the text using a scale so that it's height is increased by a multiple of the leading,
                                 * and then rotate the text before computing the bounds. The scale results in some whitespace around
                                 * the unrotated top and bottom of the text that normally wouldn't be present if unscaled, but
                                 * is Added by the standard Excel autosize.
                                 */
                                double angle = style.Rotation * 2.0 * Math.PI / 360.0;
                                //AffineTransform trans = new AffineTransform();
                                //trans.Concatenate(AffineTransform.GetRotateInstance(style.Rotation*2.0*Math.PI/360.0));
                                //trans.Concatenate(
                                //AffineTransform.GetScaleInstance(1, fontHeightMultiple)
                                //);
                                //width = Math.Max(width, ((layout.GetOutline(trans).Bounds.Width / colspan) / defaultCharWidth) + cell.CellStyle.Indention);

                                SizeF sf = g.MeasureString(txt, font);
                                double x1 = sf.Height * Math.Sin(angle);
                                double x2 = sf.Width * Math.Cos(angle);
                                double w = Math.Round(x1 + x2, 0, MidpointRounding.ToEven);
                                width = Math.Max(width, (w / colspan / defaultCharWidth) * 2 + cell.CellStyle.Indention);
                            }
                            else
                            {
                                //width = Math.Max(width, ((layout.Bounds.Width / colspan) / defaultCharWidth) + cell.CellStyle.Indention);
                                double w = Math.Round(g.MeasureString(txt, font).Width, 0, MidpointRounding.ToEven);
                                width = Math.Max(width, (w * 1.0 / colspan / defaultCharWidth) * 2 + cell.CellStyle.Indention);
                            }
                        }
                    }

                }
            }
            return width;
        }

        // /**
        // * Copy text attributes from the supplied Font to Java2D AttributedString
        // */
        //private static void copyAttributes(IFont font, AttributedString str, int startIdx, int endIdx)
        //{
        //    str.AddAttribute(TextAttribute.FAMILY, font.FontName, startIdx, endIdx);
        //    str.AddAttribute(TextAttribute.SIZE, (float)font.FontHeightInPoints);
        //    if (font.Boldweight == (short)FontBoldWeight.BOLD) str.AddAttribute(TextAttribute.WEIGHT, TextAttribute.WEIGHT_BOLD, startIdx, endIdx);
        //    if (font.IsItalic) str.AddAttribute(TextAttribute.POSTURE, TextAttribute.POSTURE_OBLIQUE, startIdx, endIdx);
        //    if (font.Underline == (byte)FontUnderlineType.SINGLE) str.AddAttribute(TextAttribute.UNDERLINE, TextAttribute.UNDERLINE_ON, startIdx, endIdx);           
        //}

        /// <summary>
        /// Convert HSSFFont to Font.
        /// </summary>
        /// <param name="font1">The font.</param>
        /// <returns></returns>
        public static Font IFont2Font(IFont font1)
        {
            FontStyle style = FontStyle.Regular;
            if (font1.Boldweight == (short)FontBoldWeight.BOLD)
            {
                style |= FontStyle.Bold;
            }
            if (font1.IsItalic)
                style |= FontStyle.Italic;
            if (font1.Underline == (byte)FontUnderlineType.SINGLE)
            {
                style |= FontStyle.Underline;
            }
            Font font = new Font(font1.FontName, font1.FontHeightInPoints, style, GraphicsUnit.Point);
            return font;
            //return new System.Drawing.Font(font1.FontName, font1.FontHeightInPoints);
        }
        public static bool ContainsCell(CellRangeAddress cr, int rowIx, int colIx)
        {
            if (cr.FirstRow <= rowIx && cr.LastRow >= rowIx
                    && cr.FirstColumn <= colIx && cr.LastColumn >= colIx)
            {
                return true;
            }
            return false;
        }

    }
}
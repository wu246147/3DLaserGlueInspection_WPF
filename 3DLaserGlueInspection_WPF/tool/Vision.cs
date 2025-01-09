using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace _3DLaserGlueInspection
{
    public class Vision
    {
        /// <summary>
        /// 仅适用无重影，无噪点，激光明显
        /// </summary>
        public Dictionary<double, double> 获取激光像素位置(HImage hImage, double minThreshold, int offsetX = 0, int offsetY = 0)
        {
            Dictionary<double, double> map = new Dictionary<double, double>();

            hImage.GetImageSize(out int width, out int height);
            HRegion lineRegion = hImage.Threshold(minThreshold, 255);
            int halfRectangle = 1;//采样宽度
            for (int i = 0; i < width; i++)
            {
                HRegion rectangle = new HRegion(0.0, i - halfRectangle, height, i + halfRectangle);
                HRegion region = rectangle.Intersection(lineRegion);
                //求区域中心
                if (region.AreaCenter(out double y, out double x) > 2)
                {
                    map.Add(i + offsetX, y + offsetY);
                }
                rectangle.Dispose();
                region.Dispose();
            }
            lineRegion.Dispose();
            return map;
        }
        /// <summary>
        /// 仅适用无重影，无噪点，激光明显
        /// </summary>
        public Dictionary<double, double> 获取激光像素位置2(HImage hImage, double minThreshold, int offsetX = 0, int offsetY = 0)
        {
            Dictionary<double, double> map0 = new Dictionary<double, double>();
            Dictionary<double, double> maphalf = new Dictionary<double, double>();

            hImage.GetImageSize(out int width, out int height);
            HRegion lineRegion = hImage.Threshold(minThreshold, 255);
            int halfRectangle = 1;//采样宽度
            int half = width / 2;
            Task task = Task.Run(() =>
            {
                for (int i = 0; i < half; i++)
                {
                    HRegion rectangle = new HRegion(0.0, i - halfRectangle, height, i + halfRectangle);
                    HRegion region = rectangle.Intersection(lineRegion);
                    //求区域中心
                    if (region.AreaCenter(out double y, out double x) > 2)
                    {
                        map0.Add(i + offsetX, y + offsetY);
                    }
                    rectangle.Dispose();
                    region.Dispose();
                }
            });

            for (int i = half; i < width; i++)
            {
                HRegion rectangle = new HRegion(0.0, i - halfRectangle, height, i + halfRectangle);
                HRegion region = rectangle.Intersection(lineRegion);
                //求区域中心
                if (region.AreaCenter(out double y, out double x) > 2)
                {
                    maphalf.Add(i + offsetX, y + offsetY);
                }
                rectangle.Dispose();
                region.Dispose();
            }
            while (!task.IsCompleted) { }
            lineRegion.Dispose();
            return map0.Concat(maphalf).ToDictionary(key => key.Key, value => value.Value);
        }

        public Dictionary<double, double> 获取激光像素位置HDR(HImage hImage, double minThreshold, int offsetX = 0, int offsetY = 0)
        {
            Dictionary<double, double> map = new Dictionary<double, double>();

            hImage.GetImageSize(out int width, out int height);
            Dictionary<double, HRegion> regionThresholds = new Dictionary<double, HRegion>();//缓存避免重复运算
            int halfRectangle = 1;//采样宽度
            for (int i = 0; i < width; i++)
            {
                HRegion rectangle = new HRegion(0.0, i - halfRectangle, height, i + halfRectangle);
                rectangle.MinMaxGray(hImage, 0.2, out double min, out double max, out double range);
                double threshold = Math.Max(max - 1, minThreshold);
                if (!regionThresholds.TryGetValue(threshold, out HRegion value))
                {
                    value = hImage.Threshold(threshold, 255);
                    regionThresholds.Add(threshold, value);
                }
                HRegion region = rectangle.Intersection(value);
                HRegion regionConnection = region.Connection();
                //寻找面积最大的
                int index = 1;
                for (int j = 2; j - 1 < regionConnection.CountObj(); j++)
                {
                    if (regionConnection[j].Area > regionConnection[index].Area)
                    {
                        index = j;
                    }
                }
                //求区域中心
                if (regionConnection[index].AreaCenter(out double y, out double x) > 2)
                {
                    map.Add(i + offsetX, y + offsetY);
                }
                rectangle.Dispose();
                region.Dispose();
                regionConnection.Dispose();
            }
            foreach (HRegion region in regionThresholds.Values)
            {
                region.Dispose();
            }
            return map;
        }
        public Dictionary<double, double> 获取激光像素位置HDR2(HImage hImage, double minThreshold, int offsetX = 0, int offsetY = 0)
        {
            Dictionary<double, double> map0 = new Dictionary<double, double>();
            Dictionary<double, double> maphalf = new Dictionary<double, double>();

            hImage.GetImageSize(out int width, out int height);
            object olock_regionThresholds = new object();
            Dictionary<double, HRegion> regionThresholds = new Dictionary<double, HRegion>();//缓存避免重复运算
            int halfRectangle = 3;//采样宽度
            int half = width / 2;
            Task task = Task.Run(() =>
            {
                for (int i = 0; i < half; i++)
                {
                    HRegion rectangle = new HRegion(0.0, i - halfRectangle, height, i + halfRectangle);
                    rectangle.MinMaxGray(hImage, 0.2, out double min, out double max, out double range);
                    double threshold = Math.Max(max - 1, minThreshold);
                    HRegion value;
                    lock (olock_regionThresholds)
                    {
                        if (!regionThresholds.TryGetValue(threshold, out value))
                        {
                            value = hImage.Threshold(threshold, 255);
                            regionThresholds.Add(threshold, value);
                        }
                    }
                    HRegion region = rectangle.Intersection(value);
                    HRegion regionConnection = region.Connection();
                    //寻找面积最大的
                    int index = 1;
                    for (int j = 2; j - 1 < regionConnection.CountObj(); j++)
                    {
                        if (regionConnection[j].Area > regionConnection[index].Area)
                        {
                            index = j;
                        }
                    }
                    //求区域中心
                    if (regionConnection[index].AreaCenter(out double y, out double x) > 2)
                    {
                        map0.Add(i + offsetX, y + offsetY);
                    }
                    rectangle.Dispose();
                    region.Dispose();
                    regionConnection.Dispose();
                }
            });

            for (int i = half; i < width; i++)
            {
                HRegion rectangle = new HRegion(0.0, i - halfRectangle, height, i + halfRectangle);
                rectangle.MinMaxGray(hImage, 0.2, out double min, out double max, out double range);
                double threshold = Math.Max(max - 1, minThreshold);
                HRegion value;
                lock (olock_regionThresholds)
                {
                    if (!regionThresholds.TryGetValue(threshold, out value))
                    {
                        value = hImage.Threshold(threshold, 255);
                        regionThresholds.Add(threshold, value);
                    }
                }
                HRegion region = rectangle.Intersection(value);
                HRegion regionConnection = region.Connection();
                //寻找面积最大的
                int index = 1;
                for (int j = 2; j - 1 < regionConnection.CountObj(); j++)
                {
                    if (regionConnection[j].Area > regionConnection[index].Area)
                    {
                        index = j;
                    }
                }
                //求区域中心
                if (regionConnection[index].AreaCenter(out double y, out double x) > 2)
                {
                    maphalf.Add(i + offsetX, y + offsetY);
                }
                rectangle.Dispose();
                region.Dispose();
                regionConnection.Dispose();
            }
            while (!task.IsCompleted) { }
            foreach (HRegion region in regionThresholds.Values)
            {
                region.Dispose();
            }
            return map0.Concat(maphalf).ToDictionary(key => key.Key, value => value.Value);
        }



        /// <summary>
        /// 输入像素坐标，输出物理坐标xy
        /// </summary>
        public void GetXY(HCamPar hCamPar, HPose hWorldPose, Dictionary<double, double> xys, out HTuple hx, out HTuple hy, bool 反转X = false, bool 反转Y = false)
        {
            GetXY(hCamPar, hWorldPose, xys.Keys.ToArray(), xys.Values.ToArray(), out hx, out hy, 反转X, 反转Y);
        }
        /// <summary>
        /// 输入像素坐标，输出物理坐标xy
        /// </summary>
        public void GetXY(HCamPar hCamPar, HPose hWorldPose, List<double> xs, List<double> ys, out HTuple hx, out HTuple hy, bool 反转X = false, bool 反转Y = false)
        {
            GetXY(hCamPar, hWorldPose, xs.ToArray(), ys.ToArray(), out hx, out hy, 反转X, 反转Y);
        }
        /// <summary>
        /// 输入像素坐标，输出物理坐标xy
        /// </summary>
        public void GetXY(HCamPar hCamPar, HPose hWorldPose, double[] xs, double[] ys, out HTuple hx, out HTuple hy, bool 反转X = false, bool 反转Y = false)
        {
            hCamPar.ImagePointsToWorldPlane(hWorldPose, new HTuple(ys), new HTuple(xs), "m", out hx, out hy);
            if (反转X)
            {
                hx = hx * -1d;
            }
            if (反转Y)
            {
                hy = hy * -1d;
            }
        }


        public HXLDCont 轮廓提取_像素(List<double> xs, List<double> ys, int 分段距离, int 成段点数, int offsetX = 0, int offsetY = 0)
        {
            //距离分段
            List<double[]> doublesX = new List<double[]>();
            List<double[]> doublesY = new List<double[]>();
            int sIndex = 0;
            for (int i = 1; i < ys.Count; i++)
            {
                double dd = Math.Sqrt(Math.Pow(xs[i] - xs[i - 1], 2) + Math.Pow(ys[i] - ys[i - 1], 2));
                if (dd > 分段距离)
                {
                    doublesX.Add(xs.Skip(sIndex).Take(i - sIndex).ToArray());
                    doublesY.Add(ys.Skip(sIndex).Take(i - sIndex).ToArray());
                    sIndex = i;
                }
            }
            doublesX.Add(xs.Skip(sIndex).ToArray());
            doublesY.Add(ys.Skip(sIndex).ToArray());
            //剔除
            doublesX.RemoveAll(n => n.Length < 成段点数);
            doublesY.RemoveAll(n => n.Length < 成段点数);
            //重组
            HTuple hTupleX = new HTuple();
            doublesX.ForEach(n => hTupleX.Append(n));
            HTuple hTupleY = new HTuple();
            doublesY.ForEach(n => hTupleY.Append(n));

            HXLDCont hXLDCont = new HXLDCont(hTupleY + offsetY, hTupleX + offsetX);

            return hXLDCont;
        }
        public HXLDCont 轮廓提取(HTuple xs, HTuple ys, double 分段距离, int 成段点数)
        {
            //距离分段
            List<double[]> doublesX = new List<double[]>();
            List<double[]> doublesY = new List<double[]>();
            int sIndex = 0;
            for (int i = 1; i < ys.Length; i++)
            {
                double dd = Math.Sqrt(Math.Pow(xs[i] - xs[i - 1], 2) + Math.Pow(ys[i] - ys[i - 1], 2));
                if (dd > 分段距离)
                {
                    doublesX.Add(xs.TupleSelectRange(sIndex, i - 1).ToDArr());
                    doublesY.Add(ys.TupleSelectRange(sIndex, i - 1).ToDArr());
                    sIndex = i;
                }
            }
            doublesX.Add(xs.TupleSelectRange(sIndex, ys.Length - 1).ToDArr());
            doublesY.Add(ys.TupleSelectRange(sIndex, ys.Length - 1).ToDArr());
            //剔除
            doublesX.RemoveAll(n => n.Length < 成段点数);
            doublesY.RemoveAll(n => n.Length < 成段点数);
            //重组
            HTuple hTupleX = new HTuple();
            doublesX.ForEach(n => hTupleX.Append(n));
            HTuple hTupleY = new HTuple();
            doublesY.ForEach(n => hTupleY.Append(n));

            HXLDCont hXLDCont = new HXLDCont(hTupleY, hTupleX);

            return hXLDCont;
        }
        public HXLDCont 轮廓提取(HTuple xs, HTuple ys, double 分段距离, int 成段点数, double 分段角度, double 分段角度距离, out HTuple 转折坐标X, out HTuple 转折坐标Y, out HTuple 转折标记)
        {
            //距离分段
            List<double[]> doublesX = new List<double[]>();
            List<double[]> doublesY = new List<double[]>();
            int sIndex = 0;
            for (int i = 1; i < ys.Length; i++)
            {
                double dd = Math.Sqrt(Math.Pow(xs[i] - xs[i - 1], 2) + Math.Pow(ys[i] - ys[i - 1], 2));
                if (dd > 分段距离)
                {
                    doublesX.Add(xs.TupleSelectRange(sIndex, i - 1).ToDArr());
                    doublesY.Add(ys.TupleSelectRange(sIndex, i - 1).ToDArr());
                    sIndex = i;
                }
            }
            doublesX.Add(xs.TupleSelectRange(sIndex, ys.Length - 1).ToDArr());
            doublesY.Add(ys.TupleSelectRange(sIndex, ys.Length - 1).ToDArr());
            //剔除
            doublesX.RemoveAll(n => n.Length < 成段点数);
            doublesY.RemoveAll(n => n.Length < 成段点数);
            //重组
            HTuple hTupleX = new HTuple();
            doublesX.ForEach(n => hTupleX.Append(n));
            HTuple hTupleY = new HTuple();
            doublesY.ForEach(n => hTupleY.Append(n));

            HXLDCont hXLDCont = new HXLDCont(hTupleY, hTupleX);

            //以下新增

            Dictionary<int, int> 角度分段下标 = new Dictionary<int, int>();
            int 角度分段起点 = -1;
            int 角度分段上一点 = -1;
            var angle = hXLDCont.GetContourAngleXld("rel", "mean", 3);
            for (int i = 0; i < angle.Length; i++)
            {
                if (Math.Abs(angle[i].D) > 分段角度)
                {
                    if (角度分段上一点 != -1)
                    {
                        double dd = Math.Sqrt(Math.Pow(hTupleX[i] - hTupleX[角度分段上一点], 2) + Math.Pow(hTupleY[i] - hTupleY[角度分段上一点], 2));
                        if (dd > 分段角度距离)
                        {
                            角度分段下标.Add(角度分段起点, 角度分段上一点);
                            角度分段起点 = i;
                        }
                    }
                    else
                    {
                        角度分段起点 = i;
                    }
                    角度分段上一点 = i;
                }
            }
            角度分段下标.Add(角度分段起点, 角度分段上一点);
            List<int> 折点下标 = new List<int>();
            foreach (var item in 角度分段下标)
            {
                if (item.Value - item.Key + 1 >= 2)
                {
                    折点下标.Add((item.Value + item.Key) / 2);
                }
            }
            int[] indexs = new int[doublesX.Count * 2];
            int indexsAdd = 0;
            for (int i = 0; i < doublesX.Count; i++)
            {
                indexs[i * 2] = indexsAdd;
                indexs[i * 2 + 1] = indexsAdd + doublesX[i].Length - 1;
                indexsAdd += doublesX[i].Length;
            }

            折点下标.AddRange(indexs);
            indexs = 折点下标.OrderBy(n => n).ToArray();

            //新重组
            HTuple newTupleX = new HTuple();
            HTuple newTupleY = new HTuple();
            Array.ForEach(indexs, i => { newTupleX.Append(hTupleX[i]); newTupleY.Append(hTupleY[i]); });
            //HXLDCont newXLDCont = new HXLDCont(newTupleY, newTupleX);

            bool[] 下位点 = Enumerable.Repeat(true, indexs.Length).ToArray();
            for (int i = 1; i < indexs.Length - 1; i++)
            {
                HOperatorSet.AngleLl(hTupleY[indexs[i - 1]], hTupleX[indexs[i - 1]], hTupleY[indexs[i]], hTupleX[indexs[i]], hTupleY[indexs[i]], hTupleX[indexs[i]], hTupleY[indexs[i + 1]], hTupleX[indexs[i + 1]], out HTuple hTuple);
                if (hTuple.D < 0)
                {
                    下位点[i] = false;
                }
            }
            for (int i = 1; i < indexs.Length - 1; i++)
            {
                if (下位点[i] && !下位点[i - 1] && !下位点[i + 1])//单点下位转上位
                {
                    下位点[i] = false;
                }
            }
            转折坐标X = newTupleX;
            转折坐标Y = newTupleY;
            转折标记 = new HTuple(Array.ConvertAll(下位点, n => n ? GlobalVarAndFunc.LanguageTranslate("下") : GlobalVarAndFunc.LanguageTranslate("上")));


            return hXLDCont;
        }
        public HXLDCont 轮廓提取(HTuple xs, HTuple ys, double 分段距离, int 成段点数, double 分段角度, double 分段角度距离, out HTuple 转折坐标X, out HTuple 转折坐标Y, out HTuple 转折标记, out HXLDCont 胶轮廓)
        {
            //距离分段
            List<double[]> doublesX = new List<double[]>();
            List<double[]> doublesY = new List<double[]>();
            int sIndex = 0;
            for (int i = 1; i < ys.Length; i++)
            {
                double dd = Math.Sqrt(Math.Pow(xs[i] - xs[i - 1], 2) + Math.Pow(ys[i] - ys[i - 1], 2));
                if (dd > 分段距离)
                {
                    doublesX.Add(xs.TupleSelectRange(sIndex, i - 1).ToDArr());
                    doublesY.Add(ys.TupleSelectRange(sIndex, i - 1).ToDArr());
                    sIndex = i;
                }
            }
            doublesX.Add(xs.TupleSelectRange(sIndex, ys.Length - 1).ToDArr());
            doublesY.Add(ys.TupleSelectRange(sIndex, ys.Length - 1).ToDArr());
            //剔除
            doublesX.RemoveAll(n => n.Length < 成段点数);
            doublesY.RemoveAll(n => n.Length < 成段点数);
            //重组
            HTuple hTupleX = new HTuple();
            doublesX.ForEach(n => hTupleX.Append(n));
            HTuple hTupleY = new HTuple();
            doublesY.ForEach(n => hTupleY.Append(n));

            HXLDCont hXLDCont = new HXLDCont(hTupleY, hTupleX);

            //以下新增

            Dictionary<int, int> 角度分段下标 = new Dictionary<int, int>();
            int 角度分段起点 = -1;
            int 角度分段上一点 = -1;
            var angle = hXLDCont.GetContourAngleXld("rel", "mean", 3);
            for (int i = 0; i < angle.Length; i++)
            {
                if (Math.Abs(angle[i].D) > 分段角度)
                {
                    if (角度分段上一点 != -1)
                    {
                        double dd = Math.Sqrt(Math.Pow(hTupleX[i] - hTupleX[角度分段上一点], 2) + Math.Pow(hTupleY[i] - hTupleY[角度分段上一点], 2));
                        if (dd > 分段角度距离)
                        {
                            角度分段下标.Add(角度分段起点, 角度分段上一点);
                            角度分段起点 = i;
                        }
                    }
                    else
                    {
                        角度分段起点 = i;
                    }
                    角度分段上一点 = i;
                }
            }
            角度分段下标.Add(角度分段起点, 角度分段上一点);
            List<int> 折点下标 = new List<int>();
            foreach (var item in 角度分段下标)
            {
                if (item.Value - item.Key + 1 >= 2)
                {
                    折点下标.Add((item.Value + item.Key) / 2);
                }
            }
            int[] indexs = new int[doublesX.Count * 2];
            int indexsAdd = 0;
            for (int i = 0; i < doublesX.Count; i++)
            {
                indexs[i * 2] = indexsAdd;
                indexs[i * 2 + 1] = indexsAdd + doublesX[i].Length - 1;
                indexsAdd += doublesX[i].Length;
            }

            折点下标.AddRange(indexs);
            indexs = 折点下标.OrderBy(n => n).ToArray();

            //新重组
            HTuple newTupleX = new HTuple();
            HTuple newTupleY = new HTuple();
            Array.ForEach(indexs, i => { newTupleX.Append(hTupleX[i]); newTupleY.Append(hTupleY[i]); });
            //HXLDCont newXLDCont = new HXLDCont(newTupleY, newTupleX);

            bool[] 下位点 = Enumerable.Repeat(true, indexs.Length).ToArray();
            for (int i = 1; i < indexs.Length - 1; i++)
            {
                HOperatorSet.AngleLl(hTupleY[indexs[i - 1]], hTupleX[indexs[i - 1]], hTupleY[indexs[i]], hTupleX[indexs[i]], hTupleY[indexs[i]], hTupleX[indexs[i]], hTupleY[indexs[i + 1]], hTupleX[indexs[i + 1]], out HTuple hTuple);
                if (hTuple.D < 0)
                {
                    下位点[i] = false;
                }
            }
            for (int i = 1; i < indexs.Length - 1; i++)
            {
                if (下位点[i] && !下位点[i - 1] && !下位点[i + 1])//单点下位转上位
                {
                    下位点[i] = false;
                }
            }
            转折坐标X = newTupleX;
            转折坐标Y = newTupleY;
            转折标记 = new HTuple(Array.ConvertAll(下位点, n => n ? GlobalVarAndFunc.LanguageTranslate("下") : GlobalVarAndFunc.LanguageTranslate("上")));

            //以下新增

            HXLDCont hXLDConts = null;
            int 下标记录 = 0;
            for (int i = 1; i < indexs.Length; i++)
            {
                if (下位点[i])
                {
                    if (!下位点[i - 1])
                    {
                        HXLDCont XLDCont = new HXLDCont(hTupleY.TupleSelectRange(indexs[下标记录], indexs[i]), hTupleX.TupleSelectRange(indexs[下标记录], indexs[i]));
                        if (hXLDConts == null)
                        {
                            hXLDConts = XLDCont;
                        }
                        else
                        {
                            var temp = hXLDConts.ConcatObj(XLDCont);
                            hXLDConts.Dispose();
                            XLDCont.Dispose();
                            hXLDConts = temp;
                        }
                    }
                    下标记录 = i;
                }
            }
            胶轮廓 = hXLDConts;

            return hXLDCont;
        }

        public void RunRegion(HRegion hRegion, ImageSet imageSet, out HRegion hRegionGenRectangle2, out Data data, out bResult bResult)
        {
            data = new Data();
            bResult = new bResult();
            hRegion.SmallestRectangle2(out data.row, out data.column, out double phi, out double length1, out double length2);
            hRegionGenRectangle2 = new HRegion();
            hRegionGenRectangle2.GenRectangle2(data.row, data.column, phi, length1, length2);

            bool heng = Math.Abs(phi) <= Math.PI / 4;
            data.胶高 = (heng ? length2 : length1) / 100d * 2;
            data.胶宽 = (heng ? length1 : length2) / 100d * 2;
            data.面积 = hRegion.Area / 10000d;
            if (data.胶高 >= imageSet.heightMin && data.胶高 <= imageSet.heightMax)
            {
                bResult.胶高 = true;
            }
            if (data.胶宽 >= imageSet.widthMin && data.胶宽 <= imageSet.widthMax)
            {
                bResult.胶宽 = true;
            }
            if (data.面积 >= imageSet.areaMin && data.面积 <= imageSet.areaMax)
            {
                bResult.面积 = true;
            }
            if (bResult.胶高 && bResult.胶宽 && bResult.面积)
            {
                bResult.Result = true;
            }
        }

        public void XLDData拆分(XLDData XLDData, int 步数, out HTuple rows, out HTuple cols)
        {
            HXLDCont hXLDCont = new HXLDCont();
            hXLDCont.GenContourNurbsXld(XLDData.ControlRows, XLDData.ControlCols, XLDData.Knots, "auto", 3, 1, 5);
            hXLDCont.GetContourXld(out HTuple row, out HTuple col);
            hXLDCont.Dispose();
            double 总长 = 0;
            List<double> 各长 = new List<double>();
            for (int i = 1; i < row.Length; i++)
            {
                double d = Math.Sqrt(Math.Pow(row[i] - row[i - 1], 2) + Math.Pow(col[i] - col[i - 1], 2));
                各长.Add(d);
                总长 += d;
            }
            double 步长 = 步数 > 1 ? 总长 / (步数 - 1) : 总长;
            rows = new double[步数];
            cols = new double[步数];
            int index各长 = 0;
            double 顶点里程 = 各长[0];
            for (int i = 0; i < rows.Length - 1; i++)
            {
                double 目标里程 = 步长 * i;
                while (目标里程 > 顶点里程)
                {
                    index各长++;
                    顶点里程 += 各长[index各长];
                }
                double 占比 = 1 - ((顶点里程 - 目标里程) / 各长[index各长]);
                rows[i] = (row[index各长 + 1] - row[index各长]) * 占比 + row[index各长];
                cols[i] = (col[index各长 + 1] - col[index各长]) * 占比 + col[index各长];
            }
            rows[rows.Length - 1] = row[row.Length - 1];
            cols[cols.Length - 1] = col[col.Length - 1];
        }
        public void XLDData拆分(XLDData XLDData, int 步数, out HTuple rows, out HTuple cols, out HTuple angles)
        {
            XLDData拆分(XLDData, 步数, out rows, out cols);
            angles = new double[步数];
            if (rows.Length >= 5)
            {
                HXLDCont XLDCont = new HXLDCont(rows, cols);
                angles = XLDCont.GetContourAngleXld("abs", "range", 1);
                XLDCont.Dispose();
            }
            else if (rows.Length >= 2)
            {
                //自己求角度
                double[] phi = new double[rows.Length];
                HOperatorSet.LineOrientation(rows[0], cols[0], rows[1], cols[1], out HTuple angle0);
                phi[0] = angle0.D;
                for (int i = 1; i < phi.Length - 1; i++)
                {
                    HXLDCont XLDCont = new HXLDCont(new HTuple(rows[i - 1].D, rows[i].D, rows[i + 1].D), new HTuple(cols[i - 1].D, cols[i].D, cols[i + 1].D));
                    XLDCont.FitLineContourXld("regression", -1, 0, 2, 2, out double rowBegin, out double colBegin, out double rowEnd, out double colEnd, out double nr, out double nc, out double dist);
                    XLDCont.Dispose();
                    HOperatorSet.LineOrientation(rowBegin, colBegin, rowEnd, colEnd, out HTuple angle);
                    phi[i] = angle.D;
                }
                HOperatorSet.LineOrientation(rows[phi.Length - 2], cols[phi.Length - 2], rows[phi.Length - 1], cols[phi.Length - 1], out HTuple angle9);
                phi[phi.Length - 1] = angle9.D;
                angles = new HTuple(phi);
            }
        }
    }
    public struct Point3D
    {
        public double X;
        public double Y;
        public double Z;
        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class Setting
    {
        public string ErrMsg => _errMsg;
        string _errMsg = string.Empty;

        public string Name;
        /// <summary>
        /// 各段参数
        /// </summary>
        public List<CutSet> CutSets = new List<CutSet>();

        //其他参数
        public OtherSet OtherSet = new OtherSet();

        //数模图
        public HImage HImage;
        public List<XLDData> XLDDatas = new List<XLDData>();

        public Setting(string name)
        {
            this.Name = name;
        }

        public bool Load()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\Project\\" + Name + "\\";
            if (!Load(basePath))
            {
                string err = _errMsg;
                string basePath_bak = AppDomain.CurrentDomain.BaseDirectory + "Data\\Project\\" + Name + "_bak\\";
                if (!Load(basePath_bak))
                {
                    _errMsg = err;
                    return false;
                }
                else
                {
                    CopyDirectory(basePath_bak, basePath);
                }
            }
            return true;
        }

        private bool Load(string basePath)
        {
            _errMsg = string.Empty;
            bool result0 = true;
            try
            {
                string fPath = basePath + "OtherSet.xml";
                if (File.Exists(fPath))
                {
                    XmlSerializer xml = new XmlSerializer(OtherSet.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Open))
                    {
                        OtherSet = (OtherSet)xml.Deserialize(stream);
                    }
                    if (OtherSet == null)
                    {
                        OtherSet = new OtherSet();
                        result0 = false;
                        _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常");
                    }
                }
                else
                {
                    _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    result0 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result0 = false;
            }

            bool result1 = true;
            try
            {
                CutSets = new List<CutSet>();
                string fPath = basePath + "CutSet.xml";
                if (File.Exists(fPath))
                {
                    XmlSerializer xml = new XmlSerializer(CutSets.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Open))
                    {
                        CutSets = (List<CutSet>)xml.Deserialize(stream);
                    }
                    if (CutSets == null)
                    {
                        CutSets = new List<CutSet>();
                        result1 = false;
                        _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常");
                    }
                }
                else
                {
                    _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    result1 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result1 = false;
            }

            bool result2 = true;
            try
            {
                string fPath = basePath + "Image.png";
                if (File.Exists(fPath))
                {
                    HImage = new HImage(fPath);
                }
                else
                {
                    _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    result2 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result2 = false;
            }

            bool result3 = true;
            try
            {
                XLDDatas = new List<XLDData>();
                string fPath = basePath + "XLDData.xml";
                if (File.Exists(fPath))
                {
                    XmlSerializer xml = new XmlSerializer(XLDDatas.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Open))
                    {
                        XLDDatas = (List<XLDData>)xml.Deserialize(stream);
                    }
                    if (XLDDatas == null)
                    {
                        XLDDatas = new List<XLDData>();
                        result3 = false;
                        _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常");
                    }
                }
                else
                {
                    _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    result3 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result3 = false;
            }

            return result0 && result1 && result2 && result3;
        }

        public bool Save()
        {
            bool result = true;

            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\Project\\" + Name + "\\";
            try
            {
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }
                {
                    string fPath = basePath + "OtherSet.xml";
                    XmlSerializer xml = new XmlSerializer(OtherSet.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Create))
                    {
                        xml.Serialize(stream, OtherSet);
                    }
                }
                {
                    string fPath = basePath + "CutSet.xml";
                    XmlSerializer xml = new XmlSerializer(CutSets.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Create))
                    {
                        xml.Serialize(stream, CutSets);
                    }
                }
                {
                    string fPath = basePath + "Image.png";
                    HImage?.WriteImage("png 1", 0, fPath);
                }
                {
                    string fPath = basePath + "XLDData.xml";
                    XmlSerializer xml = new XmlSerializer(XLDDatas.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Create))
                    {
                        xml.Serialize(stream, XLDDatas);
                    }
                }
            }
            catch (Exception ex)
            {
                result = false;
                _errMsg = ex.ToString();
            }

            if (result)
            {
                string destPath = AppDomain.CurrentDomain.BaseDirectory + "Data\\Project\\" + Name + "_bak";
                if (Directory.Exists(destPath))
                {
                    Directory.Delete(destPath, true);
                }
                CopyDirectory(basePath, destPath);
            }

            return result;
        }

        private void CopyDirectory(string sourcePath, string destPath)
        {
            string floderName = Path.GetFileName(sourcePath);
            DirectoryInfo di = Directory.CreateDirectory(Path.Combine(destPath, floderName));
            string[] files = Directory.GetFileSystemEntries(sourcePath);

            foreach (string file in files)
            {
                if (Directory.Exists(file))
                {
                    CopyDirectory(file, di.FullName);
                }
                else
                {
                    File.Copy(file, Path.Combine(di.FullName, Path.GetFileName(file)), true);
                }
            }
        }
    }

    [Serializable]
    public class OtherSet
    {
        //保存图片
        public bool SaveNGImage = true;
        public bool SaveOKImage = true;
        public string SaveImagePath = "D:\\image";
    }

    [Serializable]
    public class CutSet
    {
        public string Name;
        //图像数量
        public int ImageNum = 0;
        //相机启用情况
        public bool Cam1Enabled = true;
        public bool Cam2Enabled = true;
        public bool Cam3Enabled = true;
        public bool Cam4Enabled = true;
        //显示画布大小
        public int ShowWidth = 50;//mm
        public int ShowHeight = 50;
        //3D颜色范围
        public double ShowColorMax = 100;//mm
        public double ShowColorMin = -100;
        //标识大小
        public int Size = 3;
        public int StartImageIndex = 0;
        public int EndImageIndex = 0;

        /// <summary>
        /// 各相机-图片参数
        /// </summary>
        public List<List<ImageSet>> imageSet = new List<List<ImageSet>>();
        public CutSet(string name)
        {
            this.Name = name;
        }
        CutSet() { }
        public CutSet Clone()
        {
            CutSet clone = (CutSet)this.MemberwiseClone();
            clone.imageSet = new List<List<ImageSet>>();
            for (int i = 0; i < imageSet.Count; i++)
            {
                clone.imageSet.Add(new List<ImageSet>());
                for (int j = 0; j < imageSet[i].Count; j++)
                {
                    clone.imageSet[i].Add(imageSet[i][j].Clone());
                }
            }
            return clone;
        }
    }

    [Serializable]
    public class ImageSet
    {
        public int Index;
        //图像启用情况
        public bool 轮廓检测 = false;
        public double minThreshold = 40;
        public bool 单帧检测 = false;
        public double widthMin = 2, widthMax = 4;
        public double heightMin = 2, heightMax = 4;
        public double areaMin = 4, areaMax = 16;
        public bool 启用裁剪 = false;
        public double LeftX = 0.25, TopY = 0.25, RightX = 0.75, DownY = 0.75;
        public bool 离散去噪 = false;
        public double 分段距离 = 1.5;
        public int 成段点数 = 3;
        public bool 拐点分段 = false;
        public double 分段弧度 = 0.070;
        public double 弧度分段距离 = 2;

        public ImageSet(int index)
        {
            this.Index = index;
        }
        ImageSet() { }
        public ImageSet Clone() { return (ImageSet)this.MemberwiseClone(); }
    }

    [Serializable]
    public class XLDData
    {
        public string Name;
        //public int step = 5, halfLength = 30, halfWidth = 3, threshold = 100;
        public double[] ControlRows, ControlCols, Knots;
        public double[] Rows, Cols, Tangents;

        public XLDData(string name)
        {
            ControlRows = new double[0];
            ControlCols = new double[0];
            Knots = new double[0];
            Rows = new double[0];
            Cols = new double[0];
            Tangents = new double[0];
            this.Name = name;
        }
        XLDData() { }
        public XLDData Clone() { return (XLDData)this.MemberwiseClone(); }
    }

    [Serializable]
    public class Data
    {
        public double row, column;
        public double 胶高;
        public double 胶宽;
        public double 面积;
    }
    [Serializable]
    public class bResult
    {
        public bool 胶高;
        public bool 胶宽;
        public bool 面积;
        /// <summary>
        /// 总结果
        /// </summary>
        public bool Result;
    }
}

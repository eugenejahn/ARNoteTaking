using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;

#if UNITY_5_3 || UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif
using OpenCVForUnity;

namespace OpenCVForUnityExample
{
    /// <summary>
    /// Comic Filter Example
    /// An example of image processing (comic filter) using the Imgproc class.
    /// Referring to http://dev.classmethod.jp/smartphone/opencv-manga-2/.
    /// </summary>
    [RequireComponent(typeof(WebCamTextureToMatHelper))]


    public class Main : MonoBehaviour
    {
        /// <summary>
        /// The gray mat.
        /// </summary>
        /// 
        public RawImage targetRawImage;




        Mat grayMat;

        /// <summary>
        /// The line mat.
        /// </summary>
        Mat lineMat;

        /// <summary>
        /// The mask mat.
        /// </summary>
        Mat maskMat;

        /// <summary>
        /// The background mat.
        /// </summary>
        Mat bgMat;

        /// <summary>
        /// The dst mat.
        /// </summary>
        Mat dstMat;

        /// <summary>
        /// The gray pixels.
        /// </summary>
        byte[] grayPixels;

        /// <summary>
        /// The mask pixels.
        /// </summary>
        byte[] maskPixels;

        /// <summary>
        /// The texture.
        /// </summary>
        Texture2D texture;

        /// <summary>
        /// The webcam texture to mat helper.
        /// </summary>
        WebCamTextureToMatHelper webCamTextureToMatHelper;

        /// <summary>
        /// The FPS monitor.
        /// </summary>
        FpsMonitor fpsMonitor;






#if UNITY_ANDROID && !UNITY_EDITOR
        float rearCameraRequestedFPS;
#endif



        private static int findLargestContour(List<MatOfPoint> squares)
        {
            if (squares.Count == 0)
                return -1;
            int max_square_idx = 0;
            int currentIndex = 0;
            double max_area = 0;

            foreach (MatOfPoint square in squares)
            {   
                double area = Imgproc.contourArea(square);
                //OpenCVForUnity.Rect rect = new OpenCVForUnity.Rect();
                //rect = Imgproc.boundingRect(square);
                if (area > max_area)
                {
                    max_area = area;
                    max_square_idx = currentIndex;
                }
                currentIndex++;
            }

            return max_square_idx;
        }


        // Use this for initialization
        void Start()
        {


            fpsMonitor = GetComponent<FpsMonitor>();

            webCamTextureToMatHelper = gameObject.GetComponent<WebCamTextureToMatHelper>();

#if UNITY_ANDROID && !UNITY_EDITOR
            // Set the requestedFPS parameter to avoid the problem of the WebCamTexture image becoming low light on some Android devices. (Pixel, pixel 2)
            // https://forum.unity.com/threads/android-webcamtexture-in-low-light-only-some-models.520656/
            // https://forum.unity.com/threads/released-opencv-for-unity.277080/page-33#post-3445178
            rearCameraRequestedFPS = webCamTextureToMatHelper.requestedFPS;
            if (webCamTextureToMatHelper.requestedIsFrontFacing) {                
                webCamTextureToMatHelper.requestedFPS = 15;
                webCamTextureToMatHelper.Initialize ();
            } else {
                webCamTextureToMatHelper.Initialize ();
            }
#else
            webCamTextureToMatHelper.Initialize();
#endif
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized()
        {
            Debug.Log("OnWebCamTextureToMatHelperInitialized");

            Mat webCamTextureMat = webCamTextureToMatHelper.GetMat();

            texture = new Texture2D(webCamTextureMat.cols(), webCamTextureMat.rows(), TextureFormat.RGBA32, false);

            gameObject.GetComponent<Renderer>().material.mainTexture = texture;

            gameObject.transform.localScale = new Vector3(webCamTextureMat.cols(), webCamTextureMat.rows(), 1);

            Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

            if (fpsMonitor != null)
            {
                fpsMonitor.Add("width", webCamTextureMat.width().ToString());
                fpsMonitor.Add("height", webCamTextureMat.height().ToString());
                fpsMonitor.Add("orientation", Screen.orientation.ToString());
            }


            float width = webCamTextureMat.width();
            float height = webCamTextureMat.height();

            float widthScale = (float)Screen.width / width;
            float heightScale = (float)Screen.height / height;
            if (widthScale < heightScale)
            {
                Camera.main.orthographicSize = (width * (float)Screen.height / (float)Screen.width) / 2;
            }
            else
            {
                Camera.main.orthographicSize = height / 2;
            }
            grayMat = new Mat(webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC1);
            lineMat = new Mat(webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC1);
            maskMat = new Mat(webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC1);

            //create a striped background.
            bgMat = new Mat(webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC1, new Scalar(255));
            for (int i = 0; i < bgMat.rows() * 2.5f; i = i + 4)
            {
                Imgproc.line(bgMat, new Point(0, 0 + i), new Point(bgMat.cols(), -bgMat.cols() + i), new Scalar(0), 1);
            }

            dstMat = new Mat(webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC1);

            grayPixels = new byte[grayMat.cols() * grayMat.rows() * grayMat.channels()];
            maskPixels = new byte[maskMat.cols() * maskMat.rows() * maskMat.channels()];



        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed()
        {
            Debug.Log("OnWebCamTextureToMatHelperDisposed");

            grayMat.Dispose();
            lineMat.Dispose();
            maskMat.Dispose();

            bgMat.Dispose();
            dstMat.Dispose();

            grayPixels = null;
            maskPixels = null;

            if (texture != null)
            {
                Texture2D.Destroy(texture);
                texture = null;
            }
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode)
        {
            Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }


        //  opticla variable !!!
        // the varaible i am using 
        bool initOpticalFlow = true;
        MatOfPoint2f prevFeatures = new MatOfPoint2f();
        MatOfPoint2f currentFeatures = new MatOfPoint2f();
        Mat currentGrayMat = new Mat();
        Mat prevGrayMat = new Mat();


        List<Scalar> color = new List<Scalar>();

        // Update is called once per frame
        int maxCorners = 30,
            minDistance = 7,
            blockSize = 7;
        double qualityLevel = 0.3;
        Size winSize = new Size(150, 150);
        int maxLevel = 2;
        TermCriteria criteria = new TermCriteria(TermCriteria.EPS | TermCriteria.COUNT, 10, 0.03);

        MatOfPoint2f p0 = new MatOfPoint2f();
        MatOfPoint2f none = new MatOfPoint2f();
        MatOfPoint corners = new MatOfPoint();

        Scalar zeroEle = new Scalar(0, 0, 0, 255);

        MatOfPoint2f p1 = new MatOfPoint2f();
        MatOfByte mMOBStatus = new MatOfByte();
        MatOfFloat err = new MatOfFloat();


        MatOfPoint paperCornerMatOfPoint = new MatOfPoint();
        bool selectTarget = false;

        public Texture2D document;
        //public RawImage document; 

        void Update()
        {
            if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
            {

                Mat mainMat = webCamTextureToMatHelper.GetMat();


                if (!selectTarget) //find paper by contours 
                {
                    grayMat = new Mat();

                    // convert texture to matrix
                    mainMat.copyTo(grayMat);

                    mainMat = findPaper(mainMat);

                    // display matrix on the screen 
                    Utils.fastMatToTexture2D(mainMat, texture);

                }
                else
                { // using optical flow 


                    // set the currentGrayMat mat 
                    currentGrayMat = new Mat(mainMat.rows(), mainMat.cols(), Imgproc.COLOR_RGB2GRAY);
                    Imgproc.cvtColor(mainMat, currentGrayMat, Imgproc.COLOR_RGBA2GRAY);


                    if (initOpticalFlow == true) // doing the init setting for optical flow
                    {


                        // create 40 points 
                        Point[] points = new Point[40];
                        // set those points near the corner
                        // paperCornerMatOfPoint  is the corner of the paper
                        for (int i = 0; i < 4; i++)
                        {
                            points[i * 10] = new Point(paperCornerMatOfPoint.toList()[i].x, paperCornerMatOfPoint.toList()[i].y);
                            points[i * 10 + 1] = new Point(paperCornerMatOfPoint.toList()[i].x + 1, paperCornerMatOfPoint.toList()[i].y);
                            points[i * 10 + 2] = new Point(paperCornerMatOfPoint.toList()[i].x, paperCornerMatOfPoint.toList()[i].y + 1);
                            points[i * 10 + 3] = new Point(paperCornerMatOfPoint.toList()[i].x + 1, paperCornerMatOfPoint.toList()[i].y + 1);
                            points[i * 10 + 4] = new Point(paperCornerMatOfPoint.toList()[i].x, paperCornerMatOfPoint.toList()[i].y - 1);
                            points[i * 10 + 5] = new Point(paperCornerMatOfPoint.toList()[i].x - 1, paperCornerMatOfPoint.toList()[i].y);
                            points[i * 10 + 6] = new Point(paperCornerMatOfPoint.toList()[i].x - 2, paperCornerMatOfPoint.toList()[i].y - 1);
                            points[i * 10 + 7] = new Point(paperCornerMatOfPoint.toList()[i].x, paperCornerMatOfPoint.toList()[i].y - 2);
                            points[i * 10 + 8] = new Point(paperCornerMatOfPoint.toList()[i].x - 2, paperCornerMatOfPoint.toList()[i].y - 2);
                            points[i * 10 + 9] = new Point(paperCornerMatOfPoint.toList()[i].x + 2, paperCornerMatOfPoint.toList()[i].y + 2);
                        }

                        // make the points closer to the corners (Harris Corner Detection )
                        //Imgproc.goodFeaturesToTrack(currentGrayMat, corners, 40, qualityLevel, minDistance, none, blockSize, false, 0.04);
                        //Imgproc.goodFeaturesToTrack(currentGrayMat, corners, 40,0.05,20);

                        corners.fromArray(points);

                        prevFeatures.fromList(corners.toList());
                        currentFeatures.fromList(corners.toList());
                        prevGrayMat = currentGrayMat.clone();

                        // won't go back t again 
                        initOpticalFlow = false;


                        // not that useful lol 
                        // create random color 
                        // not working now
                        for (int i = 0; i < maxCorners; i++)
                        {
                            color.Add(new Scalar((int)(Random.value * 255), (int)(Random.value * 255),
                                                 (int)(Random.value * 255), 255));
                        }



                    }
                    else
                    {

                        // Don't want ball move
                        //currentFeatures.fromArray(prevFeatures.toArray());


                        // want ball move
                        prevFeatures.fromArray(currentFeatures.toArray());

                        // optical flow it will changes the valu of currentFeatures
                        Video.calcOpticalFlowPyrLK(prevGrayMat, currentGrayMat, prevFeatures, currentFeatures, mMOBStatus, err);
                        //Debug.Log(st.rows());

                        // change to points list 
                        List<Point> prevList = prevFeatures.toList(),
                                    nextList = currentFeatures.toList();
                        List<byte> byteStatus = mMOBStatus.toList();


                        int x = 0;
                        int y = byteStatus.Count - 1;

                        for (x = 0; x < y; x++)
                        {
                            if (byteStatus[x] == 1)
                            {
                                Point pt = nextList[x];
                                Point pt2 = prevList[x];

                                Imgproc.circle(mainMat, pt, 10, new Scalar(0, 0, 255),  - 1);

                                Imgproc.line(mainMat, pt, pt2, new Scalar(0, 0, 255));
                            }
                        }

                        // draw the data 
                        //for (int i = 0; i < prevList.Count; i++)
                        //{
                        //    //Imgproc.circle(frame, prevList[i], 5, color[10]);
                        //    Imgproc.circle(mainMat, nextList[i], 10, new Scalar(0, 0, 255), -1);

                        //    Imgproc.line(mainMat, prevList[i], nextList[i], color[20]);
                        //}


                        List<List<Point>> cornersFeatures = new List<List<Point>>(40);
                        cornersFeatures.Add(new List<Point>(10));

                        // put the corners features data into the list
                        int tmp = 0;
                        bool last = true;
                        for (int i = 0; i < nextList.Count - 1; i++)
                        {   
                            if (Mathf.Abs((float)(nextList[i].x - nextList[i + 1].x)) < 10 && Mathf.Abs((float)(nextList[i].y - nextList[i + 1].y)) < 10)
                            {
                                if (last == true)
                                {
                                    cornersFeatures[tmp].Add(nextList[i]);
                                }
                                else
                                {
                                    cornersFeatures.Add(new List<Point>(10));
                                    tmp = tmp + 1;
                                    cornersFeatures[tmp].Add(nextList[i]);
                                }
                                last = true;
                            }
                            else
                            {
                                last = false;
                            }
                        }

                        // count corners 
                        int manyCornersFeatures = 0;
                        for (int i = 0; i < cornersFeatures.Count; i++)
                        {
                            Debug.Log(cornersFeatures[i].Count);
                            if (cornersFeatures[i].Count < 5)
                            {
                                cornersFeatures.RemoveAt(i);
                            }
                            else
                            {
                                manyCornersFeatures++;
                            }
                        }

                        //Debug.Log("Length" + manyCornersFeatures);

                        // if corners equal 4 then diplay virtual docunment into the frame
                        // doing the perspective transform
                        if (manyCornersFeatures == 4)
                        {

                            Mat documentMat = new Mat(document.height, document.width, CvType.CV_8UC3);
                            Utils.texture2DToMat(document, documentMat);

                            List<Point> srcPoints = new List<Point>();
                            srcPoints.Add(new Point(0, 0));
                            srcPoints.Add(new Point(documentMat.cols(), 0));
                            srcPoints.Add(new Point(documentMat.cols(), documentMat.rows()));
                            srcPoints.Add(new Point(0, documentMat.rows()));


                            Mat srcPointsMat = Converters.vector_Point_to_Mat(srcPoints, CvType.CV_32F);


                            List<Point> dstPoints = new List<Point>() { cornersFeatures[0][0], cornersFeatures[1][0], cornersFeatures[2][0], cornersFeatures[3][0] };
                            Mat dstPointsMat = Converters.vector_Point_to_Mat(dstPoints, CvType.CV_32F);


                            //Make perspective transform
                            Mat m = Imgproc.getPerspectiveTransform(srcPointsMat, dstPointsMat);
                            Mat warpedMat = new Mat(new Size(), documentMat.type());
                            Debug.Log((cornersFeatures[1][0].x - cornersFeatures[0][0].x) + " " + (cornersFeatures[2][0].y - cornersFeatures[1][0].y));
                            Imgproc.warpPerspective(documentMat, warpedMat, m, mainMat.size(), Imgproc.INTER_LINEAR);
                            //warpedMat.convertTo(warpedMat, CvType.CV_32F);


                            //warpedMat.convertTo(warpedMat, CvType.CV_8UC3);
                            warpedMat.convertTo(warpedMat, CvType.CV_8UC3);
                            // same size as frame
                            Mat dst = new Mat(mainMat.size(), CvType.CV_8UC3);
                            //Mat dst = new Mat(frame.size(), CvType.CV_8UC3);
                            //Mat dst2 = new Mat();

                            Imgproc.cvtColor(mainMat, dst, Imgproc.COLOR_RGBA2RGB);

                            //dst.setTo(new Scalar(0, 255, 0));
                            //currentGrayMat.copyTo(dst);
                            //dst.convertTo(dst, CvType.CV_8UC3);


                            //Imgproc.cvtColor(currentGrayMat, frame, Imgproc.COLOR_GRAY2RGBA);

                            Mat img1 = new Mat();
                            Mat mask = new Mat(mainMat.size(), CvType.CV_8UC1, new Scalar(0));
                            Imgproc.cvtColor(warpedMat, img1, Imgproc.COLOR_RGB2GRAY);
                            Imgproc.Canny(img1, img1, 100, 200);
                            List<MatOfPoint> doc_contours = new List<MatOfPoint>(); ;
                            Imgproc.findContours(img1, doc_contours, new Mat(), Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_NONE);
                            Imgproc.drawContours(mask, doc_contours, -1, new Scalar(255), Core.FILLED);
                           
                            warpedMat.copyTo(dst, mask);

                            dst.convertTo(dst, CvType.CV_8UC3);

                            Debug.Log("dst" + dst.size());


                            Imgproc.cvtColor(dst, mainMat, Imgproc.COLOR_RGB2RGBA);


                            // display on the right
                            Texture2D finalTextue = new Texture2D(dst.width(), dst.height(), TextureFormat.RGB24, false);
                            Utils.matToTexture2D(dst, finalTextue);

                            targetRawImage.texture = finalTextue;

                        }


                        // current frame to old frame 
                        prevGrayMat = currentGrayMat.clone();




                        //Imgproc.cvtColor(currentGrayMat, frame, Imgproc.COLOR_GRAY2RGBA);

                        // display matrix on the screen
                        Utils.fastMatToTexture2D(mainMat, texture);
                    }

                }
            }
        }

        public void SelectTargetButton()
        {
            if (selectTarget) initOpticalFlow = true;
            selectTarget = !selectTarget;

        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy()
        {
            webCamTextureToMatHelper.Dispose();
        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick()
        {
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene("OpenCVForUnityExample");
#else
            Application.LoadLevel("OpenCVForUnityExample");
#endif
        }

        /// <summary>
        /// Raises the play button click event.
        /// </summary>
        public void OnPlayButtonClick()
        {
            webCamTextureToMatHelper.Play();
        }

        /// <summary>
        /// Raises the pause button click event.
        /// </summary>
        public void OnPauseButtonClick()
        {
            webCamTextureToMatHelper.Pause();
        }

        /// <summary>
        /// Raises the stop button click event.
        /// </summary>
        public void OnStopButtonClick()
        {
            webCamTextureToMatHelper.Stop();
        }

        /// <summary>
        /// Raises the change camera button click event.
        /// </summary>
        public void OnChangeCameraButtonClick()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!webCamTextureToMatHelper.IsFrontFacing ()) {
                rearCameraRequestedFPS = webCamTextureToMatHelper.requestedFPS;
                webCamTextureToMatHelper.Initialize (!webCamTextureToMatHelper.IsFrontFacing (), 15, webCamTextureToMatHelper.rotate90Degree);
            } else {                
                webCamTextureToMatHelper.Initialize (!webCamTextureToMatHelper.IsFrontFacing (), rearCameraRequestedFPS, webCamTextureToMatHelper.rotate90Degree);
            }
#else
            webCamTextureToMatHelper.requestedIsFrontFacing = !webCamTextureToMatHelper.IsFrontFacing();
#endif
        }


        private Mat findPaper(Mat mainMat)
        {



            Imgproc.cvtColor(grayMat, grayMat, Imgproc.COLOR_BGR2GRAY);
            // blur image
            Imgproc.GaussianBlur(grayMat, grayMat, new Size(5, 5), 0);


            grayMat.get(0, 0, grayPixels);

            for (int i = 0; i < grayPixels.Length; i++)
            {

                maskPixels[i] = 0;

                if (grayPixels[i] < 70)
                {
                    grayPixels[i] = 0;

                    //maskPixels [i] = 1;
                }
                else if (70 <= grayPixels[i] && grayPixels[i] < 120)
                {
                    grayPixels[i] = 100;

                }
                else
                {
                    grayPixels[i] = 255;
                    //maskPixels [i] = 1;
                }
            }

            grayMat.put(0, 0, grayPixels);

            //thresholding make mage blake and white
            Imgproc.threshold(grayMat, grayMat, 0, 255, Imgproc.THRESH_OTSU);

            //extract the edge image
            Imgproc.Canny(grayMat, grayMat, 50, 50);


            //prepare for finding contours
            List<MatOfPoint> contours = new List<MatOfPoint>();

            Imgproc.findContours(grayMat, contours, new Mat(), Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

            List<MatOfPoint> tmpTargets = new List<MatOfPoint>();


            for (int i = 0; i < contours.Count; i++)
            {
                MatOfPoint cp = contours[i];
                MatOfPoint2f cn = new MatOfPoint2f(cp.toArray());
                double p = Imgproc.arcLength(cn, true);

                MatOfPoint2f approx = new MatOfPoint2f();

                // lager skew greater 0.03? 
                //convert contours to readable polyagon
                Imgproc.approxPolyDP(cn, approx, 0.03 * p, true);

                //find contours with 4 points
                if (approx.toArray().Length == 4)
                {
                    MatOfPoint approxPt = new MatOfPoint();
                    approx.convertTo(approxPt, CvType.CV_32S);
                    float maxCosine = 0;
                    float rate = 0;
                    float min_length = 100000000000000;


                    for (int j = 2; j < 5; j++)
                    {
                        Vector2 v1 = new Vector2((float)(approx.toArray()[j % 4].x - approx.toArray()[j - 1].x), (float)(approx.toArray()[j % 4].y - approx.toArray()[j - 1].y));
                        Vector2 v2 = new Vector2((float)(approx.toArray()[j - 2].x - approx.toArray()[j - 1].x), (float)(approx.toArray()[j - 2].y - approx.toArray()[j - 1].y));

                        float v1_length = Mathf.Sqrt(v1.x * v1.x + v1.y * v1.y);
                        float v2_length = Mathf.Sqrt(v2.x * v2.x + v2.y * v2.y);

                        min_length = Mathf.Min(Mathf.Min((float)(v1_length), (float)v2_length), min_length);


                        if (v1_length > v2_length)
                        {
                            rate = v2_length / v1_length;
                        }
                        else
                        {
                            rate = v1_length / v2_length;
                        }



                        float angle = Mathf.Abs(Vector2.Angle(v1, v2));
                        maxCosine = Mathf.Max(maxCosine, angle);

                    }


                    if (min_length > 100 && maxCosine < 135f)//  && rate >= 0.6  maxCosine < 135f &&
                    {
                        tmpTargets.Add(approxPt);
                        //Debug.Log("Length -----------" + min_length);

                        //Debug.Log("------------rate" + rate + "---------------");
                    }
                }
            }
            if (tmpTargets.Count > 0)
            {

                // -----------------------DRAW RECTANGLE---------------------------
                //MatOfPoint2f approxCurve = new MatOfPoint2f();

                //for (int i = 0; i < tmpTargets.Count; i++)
                //{
                //    //Convert contours(i) from MatOfPoint to MatOfPoint2f
                //    MatOfPoint2f contour2f = new MatOfPoint2f(tmpTargets[i].toArray());
                //    //Processing on mMOP2f1 which is in type MatOfPoint2f
                //    double approxDistance = Imgproc.arcLength(contour2f, true) * 0.02;
                //    Imgproc.approxPolyDP(contour2f, approxCurve, approxDistance, true);

                //    //Convert back to MatOfPoint
                //    MatOfPoint points = new MatOfPoint(approxCurve.toArray());

                //    // Get bounding rect of contour
                //    OpenCVForUnity.Rect rect = Imgproc.boundingRect(points);

                //    // draw enclosing rectangle (all same color, but you could use variable i to make them unique)
                //    Imgproc.rectangle(mainMat, new Point(rect.x, rect.y), new Point(rect.x + rect.width, rect.y + rect.height), new Scalar(0, 130, 255), 3);
                //    Imgproc.rectangle(mainMat, new Point(rect.x, rect.y), new Point(rect.x + 5, rect.y + 5), new Scalar(0, 0, 255), 5);
                //    Imgproc.rectangle(mainMat, new Point(rect.x + rect.width, rect.y), new Point(rect.x + +rect.width + 5, rect.y + 5), new Scalar(0, 0, 255), 5);
                //    Imgproc.rectangle(mainMat, new Point(rect.x + rect.width, rect.y + rect.height), new Point(rect.x + +rect.width + 5, rect.y + rect.height + 5), new Scalar(0, 0, 255), 5);
                //    Imgproc.rectangle(mainMat, new Point(rect.x, rect.y + rect.height), new Point(rect.x + 5, rect.y + rect.height + 5), new Scalar(0, 0, 255), 5);

                //}
                // -----------------------DRAW RECTANGLE---------------------------



                // get the first contours

                int largestPaper = findLargestContour(tmpTargets);
                //Debug.Log(largestPaper);
                // using the largest one 
                paperCornerMatOfPoint = tmpTargets[largestPaper];


                // draw boundary 
                Imgproc.line(mainMat, paperCornerMatOfPoint.toList()[0], paperCornerMatOfPoint.toList()[1], new Scalar(0, 255, 0), 3);
                Imgproc.line(mainMat, paperCornerMatOfPoint.toList()[0], paperCornerMatOfPoint.toList()[3], new Scalar(0, 255, 0), 3);
                Imgproc.line(mainMat, paperCornerMatOfPoint.toList()[2], paperCornerMatOfPoint.toList()[3], new Scalar(0, 255, 0), 3);
                Imgproc.line(mainMat, paperCornerMatOfPoint.toList()[1], paperCornerMatOfPoint.toList()[2], new Scalar(0, 255, 0), 3);

                // extract target from the frame and adjust some angle....
                Mat srcPointsMat = Converters.vector_Point_to_Mat(paperCornerMatOfPoint.toList(), CvType.CV_32F);

                List<Point> dstPoints = new List<Point>();
                dstPoints.Add(new Point(0, 0));
                dstPoints.Add(new Point(0, 300));
                dstPoints.Add(new Point(200, 300));
                dstPoints.Add(new Point(200, 0));

                Mat dstPointsMat = Converters.vector_Point_to_Mat(dstPoints, CvType.CV_32F);
                //Make perspective transform
                Mat m = Imgproc.getPerspectiveTransform(srcPointsMat, dstPointsMat);
                Mat warpedMat = new Mat(mainMat.size(), mainMat.type());
                Imgproc.warpPerspective(mainMat, warpedMat, m, new Size(200, 300), Imgproc.INTER_LINEAR);
                warpedMat.convertTo(warpedMat, CvType.CV_8UC3);


                Texture2D finalTargetTextue = new Texture2D(warpedMat.width(), warpedMat.height(), TextureFormat.RGB24, false);
                Utils.matToTexture2D(warpedMat, finalTargetTextue);

                targetRawImage.texture = finalTargetTextue;
                //Debug.Log(paperCornerMatOfPoint.toList()[0].ToString() + " " + paperCornerMatOfPoint.toList()[1].ToString()+ " " + paperCornerMatOfPoint.toList()[2].ToString()+ " " + paperCornerMatOfPoint.toList()[3].ToString());
            }
            //--------------------------------------------------------


            return mainMat;
        }
    }
}
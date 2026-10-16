using System;

namespace DeadlyJenga.CastleSocial
{
    // Two continuous quantile tails meet at the exact requested median. The
    // tail extent/exponent is solved against analytic variance, not a location
    // parameter pretending to be a median. Skew controls relative tail curvature.
    [Serializable]
    public sealed class SocialDistribution
    {
        public double median=50, deviation=18, skew, minimum, maximum=100;
        public SocialDistribution() { }
        public SocialDistribution(double m,double sd,double min,double max,double lean=0)
        {median=m;deviation=sd;minimum=min;maximum=max;skew=lean;}
        public CalibratedDistribution Calibrate() => new CalibratedDistribution(this);
    }
    public sealed class CalibratedDistribution
    {
        public readonly double Median,Deviation,Mean,Skewness,Minimum,Maximum;
        readonly double left,right,a,b;
        public CalibratedDistribution(SocialDistribution s)
        {
            if(!Finite(s.median)||!Finite(s.deviation)||!Finite(s.minimum)||!Finite(s.maximum)||!Finite(s.skew)||s.minimum>s.maximum||s.median<s.minimum||s.median>s.maximum||s.deviation<0||Math.Abs(s.skew)>3)
                throw new ArgumentException("Invalid bounds, median, deviation, or lean (allowed -3..3).");
            Median=s.median;Minimum=s.minimum;Maximum=s.maximum;left=Median-Minimum;right=Maximum-Median;
            if(s.deviation==0){a=b=double.PositiveInfinity;Mean=Median;return;}
            if(left==0||right==0||s.deviation>=(Maximum-Minimum)*.5)
                throw new ArgumentException("Nonzero spread needs an interior median and SD below half the range.");
            double l=1e-8,h=1;
            if(Variance(Math.Exp(s.skew),Math.Exp(-s.skew))<s.deviation*s.deviation)
                for(int i=0;i<100;i++){double p=(l+h)*.5;double x=p*Math.Exp(s.skew),y=p*Math.Exp(-s.skew);if(Variance(x,y)>s.deviation*s.deviation)l=p;else h=p;}
            else l=1;
            a=(l+h)*.5*Math.Exp(s.skew);b=(l+h)*.5*Math.Exp(-s.skew);
            double scale=s.deviation/Math.Sqrt(Variance(a,b));left*=scale;right*=scale;
            double m=Moment(1,a,b),v=Variance(a,b);Mean=Median+m;Deviation=Math.Sqrt(Math.Max(0,v));
            Skewness=(Moment(3,a,b)-3*m*Moment(2,a,b)+2*m*m*m)/Math.Pow(Math.Max(1e-12,Deviation),3);
            if(Math.Abs(Deviation-s.deviation)>Math.Max(.0001,s.deviation*.001))throw new ArgumentException("Requested spread is numerically infeasible for these bounds/lean.");
        }
        double Moment(int k,double x,double y)=>.5*(Math.Pow(-left,k)/(k*x+1)+Math.Pow(right,k)/(k*y+1));
        double Variance(double x,double y){double m=Moment(1,x,y);return Moment(2,x,y)-m*m;}
        public double Quantile(double u)
        {
            if(double.IsInfinity(a))return Median;
            u=Math.Max(0,Math.Min(1,u));return u<=.5?Median-left*Math.Pow(1-2*u,a):Median+right*Math.Pow(2*u-1,b);
        }
        public double Cdf(double value)
        {
            if(double.IsInfinity(a))return value<Median?0:1;
            if(value<=Median-left)return 0;if(value>=Median+right)return 1;
            return value<=Median?.5*(1-Math.Pow((Median-value)/left,1/a)):.5*(1+Math.Pow((value-Median)/right,1/b));
        }
        public double Sample(Random random)=>Quantile(random.NextDouble());
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
    }
    public static class SocialRandom
    {
        public static double Normal(Random r)=>Math.Sqrt(-2*Math.Log(Math.Max(1e-12,r.NextDouble())))*Math.Cos(2*Math.PI*r.NextDouble());
        public static double NormalCdf(double x)
        {double t=1/(1+.2316419*Math.Abs(x));double d=.3989422804014327*Math.Exp(-x*x/2);double p=1-d*t*(.319381530+t*(-.356563782+t*(1.781477937+t*(-1.821255978+t*1.330274429))));return x>=0?p:1-p;}
        // Cholesky for positive semidefinite correlation matrices, including
        // perfect correlations. Do not silently repair an invalid designer matrix.
        public static double[,] Factor(double[] matrix,int count)
        {
            if(matrix==null||matrix.Length!=count*count)throw new ArgumentException("Correlation matrix size does not match traits.");
            var l=new double[count,count];
            for(int i=0;i<count;i++)for(int j=0;j<count;j++)
                if(double.IsNaN(matrix[i*count+j])||Math.Abs(matrix[i*count+j])>1||Math.Abs(matrix[i*count+j]-matrix[j*count+i])>1e-7||i==j&&Math.Abs(matrix[i*count+j]-1)>1e-7)throw new ArgumentException("Correlation matrix must be symmetric, bounded, with diagonal 1.");
            for(int i=0;i<count;i++)for(int j=0;j<=i;j++)
            {double v=matrix[i*count+j];for(int k=0;k<j;k++)v-=l[i,k]*l[j,k];if(i==j){if(v< -1e-8)throw new ArgumentException("Correlation matrix is not positive semidefinite.");l[i,j]=Math.Sqrt(Math.Max(0,v));}else if(l[j,j]>1e-9)l[i,j]=v/l[j,j];else if(Math.Abs(v)>1e-8)throw new ArgumentException("Inconsistent singular correlation matrix.");}
            return l;
        }
    }
}

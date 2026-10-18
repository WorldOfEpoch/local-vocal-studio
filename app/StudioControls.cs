using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace VocalUI {
public class Knob : Control {
 int min,max=100,current,dragValue,dragY,dragX;public event EventHandler ValueChanged;
 public int Minimum {get{return min;}set{min=value;Value=current;}}
 public int Maximum {get{return max;}set{max=value;Value=current;}}
 public int Value {get{return current;}set{int next=Math.Max(min,Math.Min(max,value));if(next==current)return;current=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
 public string TickStyle{get;set;}
 public Knob(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);BackColor=Color.FromArgb(47,49,57);ForeColor=Color.FromArgb(52,222,194);TabStop=true;AccessibleRole=AccessibleRole.Slider;}
 protected override AccessibleObject CreateAccessibilityInstance(){return new KnobAccess(this);}
 sealed class KnobAccess:ControlAccessibleObject {Knob owner;public KnobAccess(Knob o):base(o){owner=o;}public override AccessibleRole Role{get{return AccessibleRole.Slider;}}public override string Value{get{return owner.Value.ToString();}set{int n;if(int.TryParse(value,out n))owner.Value=n;}}}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float f=(current-min)/(float)Math.Max(1,max-min);var circle=new RectangleF(3,3,34,34);using(var b=new SolidBrush(Color.FromArgb(33,35,41)))g.FillEllipse(b,circle);using(var p=new Pen(Color.FromArgb(77,80,87),2))g.DrawArc(p,circle,135,270);using(var p=new Pen(ForeColor,2))g.DrawArc(p,circle,135,Math.Max(.1f,f*270));double a=(135+f*270)*Math.PI/180;using(var p=new Pen(ForeColor,2))g.DrawLine(p,20,20,20+(float)Math.Cos(a)*13,20+(float)Math.Sin(a)*13);using(var p=new Pen(Color.FromArgb(70,73,82),1))g.DrawLine(p,52,20,Width-6,20);using(var b=new SolidBrush(ForeColor))g.FillRectangle(b,52+f*(Width-62),16,3,9);if(Focused)using(var p=new Pen(Color.FromArgb(100,105,112),1))g.DrawRectangle(p,0,0,Width-1,Height-1);}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();Capture=true;dragValue=current;dragY=e.Y;dragX=e.X;}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture)Value=dragValue+(int)Math.Round((dragY-e.Y+(e.X-dragX)*.4)*(max-min)/140.0);}
 protected override void OnMouseUp(MouseEventArgs e){Capture=false;base.OnMouseUp(e);}
 protected override void OnMouseWheel(MouseEventArgs e){Value+=Math.Sign(e.Delta);base.OnMouseWheel(e);}
 protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||key==Keys.Home||key==Keys.End||base.IsInputKey(key);}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Home)Value=min;else if(e.KeyCode==Keys.End)Value=max;else if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down)Value--;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up)Value++;else base.OnKeyDown(e);}
}
public class Meter:Control {int value;public int Maximum{get;set;}=600;public int Value{get{return value;}set{this.value=Math.Max(0,Math.Min(Maximum,value));Invalidate();}}public Meter(){DoubleBuffered=true;}protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.FromArgb(25,27,32));int w=(int)(Width*value/(float)Maximum);for(int x=2;x<w;x+=6){Color color=x>Width*.90?Color.Salmon:x>Width*.75?Color.Khaki:Color.FromArgb(52,222,194);using(var b=new SolidBrush(color))e.Graphics.FillRectangle(b,x,3,4,Math.Max(1,Height-6));}}}
public class Seek:Control {int value;public int Minimum{get;set;}public int Maximum{get;set;}=1000;public string TickStyle{get;set;}public int Value{get{return value;}set{this.value=Math.Max(Minimum,Math.Min(Maximum,value));Invalidate();}}public Seek(){DoubleBuffered=true;}protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.FromArgb(47,49,57));using(var p=new Pen(Color.FromArgb(25,27,32),3))e.Graphics.DrawLine(p,2,Height/2,Width-2,Height/2);int x=2+(int)((Width-4)*value/(double)Math.Max(1,Maximum));using(var p=new Pen(Color.FromArgb(52,222,194),2))e.Graphics.DrawLine(p,2,Height/2,x,Height/2);using(var b=new SolidBrush(Color.FromArgb(52,222,194)))e.Graphics.FillRectangle(b,x-2,Height/2-5,4,10);}void SetX(int x){Value=(int)(Math.Max(0,Math.Min(Width,x))*Maximum/(double)Math.Max(1,Width));}protected override void OnMouseDown(MouseEventArgs e){Capture=true;SetX(e.X);base.OnMouseDown(e);}protected override void OnMouseMove(MouseEventArgs e){if(Capture)SetX(e.X);base.OnMouseMove(e);}protected override void OnMouseUp(MouseEventArgs e){SetX(e.X);Capture=false;base.OnMouseUp(e);}}
}

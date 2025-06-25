namespace AutoUpload.WinForm.Controls;
public class HideTabHeaderTabControl : TabControl
{
    protected override void WndProc(ref Message m)
    {
        // TCM_ADJUSTRECT 消息，隐藏标签头
        if (m.Msg == 0x1328 && !DesignMode)
        {
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }
}


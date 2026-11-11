using System;
namespace CyanPreview
{
    public static class TrayGlyph
    {
        // Three small pixel glyphs, kept crisp at the tray's native icon size.
        static readonly string[] Rows = { "10000100000", "10000000000", "10000100111", "10000100010", "11100100111" };
        public static byte[] Pixels(int size)
        {
            if(size<16 || size>128) throw new ArgumentOutOfRangeException(nameof(size));
            var pixels=new byte[size*size*4];
            int scale=Math.Max(1,size/12), left=(size-11*scale)/2, top=(size-5*scale)/2;
            for(int pass=0;pass<2;pass++)
            for(int y=0;y<5;y++) for(int x=0;x<11;x++)
            {
                if(Rows[y][x]!='1') continue;
                int border=pass==0?1:0;
                for(int dy=-border;dy<scale+border;dy++) for(int dx=-border;dx<scale+border;dx++)
                {
                    int px=left+x*scale+dx, py=top+y*scale+dy;
                    if(px<0 || py<0 || px>=size || py>=size) continue;
                    int i=(py*size+px)*4;
                    pixels[i]=(byte)(pass==0?26:216); pixels[i+1]=(byte)(pass==0?24:230);
                    pixels[i+2]=(byte)(pass==0?15:64); pixels[i+3]=255;
                }
            }
            return pixels;
        }
    }
}

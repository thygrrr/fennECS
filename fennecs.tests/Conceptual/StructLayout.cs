using System.Runtime.InteropServices;

namespace fennecs.tests.Conceptual;

public class StructLayout
{
    
    [Fact]
    public void HashCodeIdentical()
    {
        var a = new StructAuto(1234u);
        var b = new StructExplicit(1234u);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(a.GetHashCode(), 1234u.GetHashCode());
    }
    
    [StructLayout(LayoutKind.Auto)]
    public record struct StructAuto(ulong raw)
    {
        public int index32
        {
            get => (int)(raw & 0xFFFFFFFFu);
            set => raw = (raw & 0xFFFFFFFF00000000u) | (uint)value;
        }
        
        public short header
        {
            get => (short)((raw & 0xFFFF000000000000u)>>48);
            set => raw = (raw & 0x0000FFFFFFFFFFFFu) | ((ulong)value << 48);
        }
        
        public int index64
        {
            get => (int)(raw & 0xFFFFFFFFu);
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            set => raw = (raw & 0xFFFFFFFF00000000u) | (ulong) value;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
        }
        
        public TypeKind kind
        {
            get => (TypeKind)((raw & 0xF0000000u) >> 28);
            set => raw = ((ulong) value & 0xF) << 28;
        }

        public override int GetHashCode()
        {
            return raw.GetHashCode();
        }
    }


    [StructLayout(LayoutKind.Explicit)]
    public record struct StructExplicit(ulong _raw)
    {
        [FieldOffset(0)]
        private ulong _raw = _raw;

        [FieldOffset(0)]
        public int index;

        [FieldOffset(6)]
        public short header;
        
        public TypeKind kind
        {
            get => (TypeKind)((_raw & 0xF0000000u) >> 28);
            set => _raw = ((ulong) value & 0xF) << 28;
        }
        
        public override int GetHashCode()
        {
            return _raw.GetHashCode();
        }
    }

}

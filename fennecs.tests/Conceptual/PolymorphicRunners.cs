namespace fennecs.tests.Conceptual;

public class PolymorphicRunners(ITestOutputHelper output)
{
    interface IDamageable
    {
        void TakeDamage(int damage);
    }
    
    class Health(int hp) : IDamageable
    {
        public int Hp = hp;
        public void TakeDamage(int damage)
        {
            Hp -= damage;
        }
    }
    
    class Armor(int ap) : IDamageable
    {
        public int Ap = ap;

        public void TakeDamage(int damage) => Ap -= damage;
    }
    

    [Fact]
    public void PolymorphicDispatch()
    {
        using var world = new World();
        var bodies = world.Query<Health>().Stream();
        var armors = world.Query<Armor>().Stream();

        for (var i = 0; i < 5; i++) world.Spawn().Add(new Health(42)).Add(new Armor(69));

        bodies.For(uniform: 5, action: (int d,  ref Health h) => DamageAction(d, h));
        armors.For(uniform: 1, action: (int d,  ref Armor a) => DamageAction(d, a));
        
        bodies.For((ref Health h) => Assert.Equal(37, h.Hp));
        armors.For((ref Armor a) => Assert.Equal(68, a.Ap));
    }
    
    void DamageAction(int damage, IDamageable damageable)
    {
        damageable.TakeDamage(damage);
    }

}
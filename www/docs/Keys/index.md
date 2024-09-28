---
title: Keys
order: 4
---
# Secondary Keys

![a fennec carrying a golden key](https://fennecs.tech/img/fennec-key.png)

**fenn**ecs allows component types, which are the primary keys, to also reference an additional secondary key.

Secondary keys may be: 
- nothing, in which case the component is a ***Plain Component***.
- a target Entity (designating a Relation to this target)
- a target Object (constituting an Object Link) 
- a Hash (a strongly typed hash code from a C# type of your choice)

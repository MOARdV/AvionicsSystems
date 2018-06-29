/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2018 MOARdV
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 * 
 ****************************************************************************/
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace AvionicsSystems
{
    // This delegate must be used for any method that uses out or ref parameters.
    // The other methods do not support 'out' and 'ref'.
    public delegate TResult DynamicMethodDelegate<TResult>(object param0, object[] param1);

    /// <summary>
    /// The DynamicMethodFactory provides a way to generate delegates where one
    /// or more parameters are typed in other assemblies.
    /// 
    /// The code is based on source found at
    /// http://www.codeproject.com/Articles/10951/Fast-late-bound-invocation-through-DynamicMethod-d
    /// which is covered by The Code Project Open License http://www.codeproject.com/info/cpol10.aspx
    /// 
    /// CreateGetField and CreateSetField based on code found here:
    /// https://stackoverflow.com/questions/16073091/is-there-a-way-to-create-a-delegate-to-get-and-set-values-for-a-fieldinfo
    /// </summary>
    static internal class DynamicMethodFactory
    {
        /// <summary>
        /// Create a delegate that returns the value of a field reflected in FieldInfo.
        /// </summary>
        /// <typeparam name="Tinstance">Instance of the variable</typeparam>
        /// <typeparam name="TResult">Type to return</typeparam>
        /// <param name="field">FieldInfo for the field of interest.</param>
        /// <returns>A delegate to fetch the value.</returns>
        static internal Func<Tinstance, TResult> CreateGetField<Tinstance, TResult>(FieldInfo field)
        {
            string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
            DynamicMethod setterMethod = new DynamicMethod(methodName, typeof(TResult), new Type[1] { typeof(Tinstance) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
            }
            // If result is of value type it needs to be boxed if
            // we're returning a generic object
            if (field.FieldType.IsValueType && typeof(TResult) == typeof(object))
            {
                gen.Emit(OpCodes.Box, field.FieldType);
            }
            gen.Emit(OpCodes.Ret);
            return (Func<Tinstance, TResult>)setterMethod.CreateDelegate(typeof(Func<Tinstance, TResult>));
        }

        /// <summary>
        /// Create a delegate that sets the value of a field reflected in FieldInfo
        /// </summary>
        /// <typeparam name="Tinstance">Instance of the variable</typeparam>
        /// <typeparam name="Tvalue">value</typeparam>
        /// <param name="field">FieldInfo for the field of interest.</param>
        /// <returns>A delegate to set the value</returns>
        static internal Action<Tinstance, Tvalue> CreateSetField<Tinstance, Tvalue>(FieldInfo field)
        {
            string methodName = field.ReflectedType.FullName + ".set_" + field.Name;
            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(Tinstance), typeof(Tvalue) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, field);
            }
            gen.Emit(OpCodes.Ret);
            return (Action<Tinstance, Tvalue>)setterMethod.CreateDelegate(typeof(Action<Tinstance, Tvalue>));
        }

        /// <summary>
        /// Create a delegate who takes a single parameter and returns nothing.
        /// </summary>
        /// <typeparam name="T">Type of the first/only parameter.</typeparam>
        /// <param name="methodInfo">MethodInfo describing the method we're calling.</param>
        /// <returns>Action delegate</returns>
        static internal Action<T> CreateAction<T>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 1)
                {
                    throw new ArgumentException("CreateAction<T> called with static method that takes " + parms.Length + " parameters");
                }

                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
            }
            else
            {
                if (parms.Length != 0)
                {
                    throw new ArgumentException("CreateAction<T> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
            }

            Type[] _argTypes = { typeof(T) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                typeof(void), // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (Action<T>)dynam.CreateDelegate(typeof(Action<T>));
        }

        /// <summary>
        /// Create a delegate who takes a single parameter and returns an object of TResult.
        /// </summary>
        /// <typeparam name="T">Type of the first/only parameter.</typeparam>
        /// <typeparam name="TResult">Type of the return value.</typeparam>
        /// <param name="methodInfo">MethodInfo describing the method we're calling.</param>
        /// <returns>Function delegate</returns>
        static internal Func<T, TResult> CreateFunc<T, TResult>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 1)
                {
                    throw new ArgumentException("CreateFunc<T, TResult> called with static method that takes " + parms.Length + " parameters");
                }

                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
            }
            else
            {
                if (parms.Length != 0)
                {
                    throw new ArgumentException("CreateFunc<T, TResult> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
            }
            if (methodInfo.ReturnType != typeof(TResult) && typeof(TResult) != typeof(object))
            {
                throw new ArgumentException("CreateFunc<T, TResult> called with mismatched return types");
            }

            Type[] _argTypes = { typeof(T) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                typeof(TResult), // return type
                //methodInfo.ReturnType, // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            if (methodInfo.ReturnType != typeof(void))
            {
                // If result is of value type it needs to be boxed if
                // we're returning a generic object
                if (methodInfo.ReturnType.IsValueType && typeof(TResult) == typeof(object))
                {
                    il.Emit(OpCodes.Box, methodInfo.ReturnType);
                }
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (Func<T, TResult>)dynam.CreateDelegate(typeof(Func<T, TResult>));
        }

        /// <summary>
        /// Create a delegate who takes a two parameters and returns nothing.
        /// </summary>
        /// <typeparam name="T">Type of the first parameter.</typeparam>
        /// <typeparam name="U">Type of the second parameter.</typeparam>
        /// <param name="methodInfo">MethodInfo describing the method we're calling.</param>
        /// <returns>Action delegate</returns>
        static internal Action<T, U> CreateAction<T, U>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 2)
                {
                    throw new ArgumentException("CreateAction<T,U> called with static method that takes " + parms.Length + " parameters");
                }

                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
                //if (typeof(U) != parms[1].ParameterType)
                //{
                //    // What to do?
                //}
            }
            else
            {
                if (parms.Length != 1)
                {
                    throw new ArgumentException("CreateAction<T,U> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
            }

            Type[] _argTypes = { typeof(T), typeof(U) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                typeof(void), // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (Action<T,U>)dynam.CreateDelegate(typeof(Action<T,U>));
        }

        /// <summary>
        /// Create a function that takes two typed parameters and returns a TResult
        /// (which may be null if the method returns void).
        /// </summary>
        /// <typeparam name="T">Type of the first parameter.</typeparam>
        /// <typeparam name="U">Type of the second parameter.</typeparam>
        /// <typeparam name="TResult">Type of the return value.</typeparam>
        /// <param name="methodInfo">MethodInfo describing the method we're calling.</param>
        /// <returns>Function delegate</returns>
        static internal Func<T, U, TResult> CreateDynFunc<T, U, TResult>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 2)
                {
                    throw new ArgumentException("CreateFunc<T, U, TResult> called with static method that takes " + parms.Length + " parameters");
                }

                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
                //if (typeof(U) != parms[1].ParameterType)
                //{
                //    // What to do?
                //}
            }
            else
            {
                if (parms.Length != 1)
                {
                    throw new ArgumentException("CreateFunc<T, U, TResult> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
                //if (typeof(U) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
            }

            if (methodInfo.ReturnType != typeof(TResult) && typeof(TResult) != typeof(object))
            {
                throw new ArgumentException("CreateFunc<T, U, TResult> called with mismatched return types");
            }

            Type[] _argTypes = { typeof(T), typeof(U) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                typeof(TResult), // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            if (methodInfo.ReturnType != typeof(void))
            {
                // If result is of value type it needs to be boxed
                if (methodInfo.ReturnType.IsValueType && typeof(TResult) == typeof(object))
                {
                    il.Emit(OpCodes.Box, methodInfo.ReturnType);
                }
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);

            return (Func<T, U, TResult>)dynam.CreateDelegate(typeof(Func<T, U, TResult>));
        }

        /// <summary>
        /// Create a function that takes three typed parameters and returns a TResult
        /// (which may be null if the method returns void).
        /// </summary>
        /// <typeparam name="T">Type of the first parameter.</typeparam>
        /// <typeparam name="U">Type of the second parameter.</typeparam>
        /// <typeparam name="V">Type of the third parameter.</typeparam>
        /// <typeparam name="TResult">Type of the return value.</typeparam>
        /// <param name="methodInfo">MethodInfo describing the method we're calling.</param>
        /// <returns>Function delegate</returns>
        static internal Func<T, U, V, TResult> CreateFunc<T, U, V, TResult>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 3)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, TResult> called with static method that takes " + parms.Length + " parameters");
                }

                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, TResult> parameter [0] mismatch");
                //}
                //if (typeof(U) != parms[1].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, TResult> parameter [1] mismatch");
                //}
                //if (typeof(V) != parms[2].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, TResult> parameter [2] mismatch");
                //}
            }
            else
            {
                if (parms.Length != 2)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, TResult> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
                //if (typeof(U) != parms[0].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, TResult> parameter [0] mismatch");
                //}
                //if (typeof(V) != parms[1].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, TResult> parameter [1] mismatch");
                //}
            }

            if (methodInfo.ReturnType != typeof(TResult) && typeof(TResult) != typeof(object))
            {
                throw new ArgumentException("CreateFunc<T, U, TResult> called with mismatched return types");
            }

            Type[] _argTypes = { typeof(T), typeof(U), typeof(V) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                typeof(TResult), // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            if (methodInfo.ReturnType != typeof(void))
            {
                // If result is of value type it needs to be boxed
                if (methodInfo.ReturnType.IsValueType && typeof(TResult) == typeof(object))
                {
                    il.Emit(OpCodes.Box, methodInfo.ReturnType);
                }
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (Func<T, U, V, TResult>)dynam.CreateDelegate(typeof(Func<T, U, V, TResult>));
        }

        /// <summary>
        /// Create a function that takes four typed parameters and returns a TResult
        /// (which may be null if the method returns void).
        /// </summary>
        /// <typeparam name="T">Type of the first parameter.</typeparam>
        /// <typeparam name="U">Type of the second parameter.</typeparam>
        /// <typeparam name="V">Type of the third parameter.</typeparam>
        /// <typeparam name="W">Type of the final parameter.</typeparam>
        /// <typeparam name="TResult">Type of the return value.</typeparam>
        /// <param name="methodInfo">MethodInfo describing the method we're calling.</param>
        /// <returns>Function delegate</returns>
        static internal Func<T, U, V, W, TResult> CreateFunc<T, U, V, W, TResult>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 4)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W, TResult> called with static method that takes " + parms.Length + " parameters");
                }

                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, W, TResult> parameter [0] mismatch");
                //}
                //if (typeof(U) != parms[1].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, W, TResult> parameter [1] mismatch");
                //}
                //if (typeof(V) != parms[2].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, W, TResult> parameter [2] mismatch");
                //}
                //if (typeof(W) != parms[3].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, W, TResult> parameter [3] mismatch");
                //}
            }
            else
            {
                if (parms.Length != 3)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W, TResult> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
                //if (typeof(U) != parms[0].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, W, TResult> parameter [0] mismatch");
                //}
                //if (typeof(V) != parms[1].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, W, TResult> parameter [1] mismatch");
                //}
                //if (typeof(W) != parms[2].ParameterType)
                //{
                //    throw new ArgumentException("CreateFunc<T, U, V, W, TResult> parameter [2] mismatch");
                //}
            }

            if (methodInfo.ReturnType != typeof(TResult) && typeof(TResult) != typeof(object))
            {
                throw new ArgumentException("CreateFunc<T, U, V, W, TResult> called with mismatched return types");
            }

            Type[] _argTypes = { typeof(T), typeof(U), typeof(V), typeof(W) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                typeof(TResult), // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            if (methodInfo.ReturnType != typeof(void))
            {
                // If result is of value type it needs to be boxed
                if (methodInfo.ReturnType.IsValueType && typeof(TResult) == typeof(object))
                {
                    il.Emit(OpCodes.Box, methodInfo.ReturnType);
                }
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (Func<T, U, V, W, TResult>)dynam.CreateDelegate(typeof(Func<T, U, V, W, TResult>));
        }

        // This function comes from http://www.codeproject.com/Articles/10951/Fast-late-bound-invocation-through-DynamicMethod-d
        // covered by The Code Project Open License  http://www.codeproject.com/info/cpol10.aspx
        //
        // Changes to support out / reference parameters from
        // http://stackoverflow.com/questions/29131117/using-ilgenerator-emit-to-call-a-method-in-another-assembly-that-has-an-out-para
        // with a fix to the code that copies values into locals.
        static internal DynamicMethodDelegate<TResult> CreateFunc<TResult>(MethodInfo methodInfo)
        {
            ParameterInfo[] parms = methodInfo.GetParameters();
            int numparams = parms.Length;

            Type[] _argTypes = { typeof(object), typeof(object[]) };

            if (methodInfo.ReturnType != typeof(TResult) && typeof(TResult) != typeof(object))
            {
                throw new ArgumentException("CreateFunc<TResult> called with mismatched return types");
            }

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "",
                typeof(TResult),
                _argTypes,
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            /* [...IL GENERATION...] */
            // Define a label for succesfull argument count checking.
            Label argsOK = il.DefineLabel();

            // Check input argument count.
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Ldc_I4, numparams);
            il.Emit(OpCodes.Beq, argsOK);

            // Argument count was wrong, throw TargetParameterCountException.
            il.Emit(OpCodes.Newobj,
               typeof(TargetParameterCountException).GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Throw);

            // Mark IL with argsOK label.
            il.MarkLabel(argsOK);
            // If method isn't static push target instance on top
            // of stack.
            if (!methodInfo.IsStatic)
            {
                // Argument 0 of dynamic method is target instance.
                il.Emit(OpCodes.Ldarg_0);
            }
            // Lay out args array onto stack.
            LocalBuilder[] locals = new LocalBuilder[parms.Length];
            for (int i = 0; i < numparams; i++)
            {
                // Push args array reference onto the stack, followed
                // by the current argument index (i). The Ldelem_Ref opcode
                // will resolve them to args[i].

                // Argument 1 of dynamic method is argument array.
                if (!parms[i].IsOut)
                {
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldelem_Ref);
                }

                // If parameter [i] is a value type perform an unboxing.
                Type parmType = parms[i].ParameterType;
                if (parmType.IsValueType)
                {
                    il.Emit(OpCodes.Unbox_Any, parmType);
                }
            }

            for (int i = 0; i < numparams; i++)
            {
                if (parms[i].IsOut)
                {
                    locals[i] = il.DeclareLocal(parms[i].ParameterType.GetElementType());
                    il.Emit(OpCodes.Ldloca, locals[i]);
                    //il.Emit(OpCodes.Ldloca, locals[locals.Length - 1]);
                }
            }

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            for (int i = 0; i < numparams; i++)
            {
                if (parms[i].IsOut || parms[i].ParameterType.IsByRef)
                {
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldloc, locals[i].LocalIndex);

                    if (parms[i].ParameterType.GetElementType().IsValueType)
                    {
                        il.Emit(OpCodes.Box, parms[i].ParameterType.GetElementType());
                    }

                    il.Emit(OpCodes.Stelem_Ref);
                }
            }

            if (methodInfo.ReturnType != typeof(void))
            {
                // If result is of value type it needs to be boxed
                if (methodInfo.ReturnType.IsValueType && typeof(TResult) == typeof(object))
                {
                    il.Emit(OpCodes.Box, methodInfo.ReturnType);
                }
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (DynamicMethodDelegate<TResult>)dynam.CreateDelegate(typeof(DynamicMethodDelegate<TResult>));
        }
    }
}

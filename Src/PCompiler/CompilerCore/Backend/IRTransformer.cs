﻿using Plang.Compiler.Backend.ASTExt;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Plang.Compiler.Backend
{
    public class IRTransformer
    {
        private readonly Function function;
        private int numTemp;

        private IRTransformer(Function function)
        {
            this.function = function;
        }

        public static void SimplifyMethod(Function function)
        {
            if (function.IsForeign)
            {
                return;
            }

            IRTransformer transformer = new IRTransformer(function);
            IPStmt functionBody = function.Body;
            function.Body = new CompoundStmt(functionBody.SourceLocation, transformer.SimplifyStatement(functionBody));
        }

        private (VariableAccessExpr, IPStmt) SaveInTemporary(IPExpr expr)
        {
            return SaveInTemporary(expr, expr.Type);
        }

        private (VariableAccessExpr, IPStmt) SaveInTemporary(IPExpr expr, PLanguageType tempType)
        {
            Antlr4.Runtime.ParserRuleContext location = expr.SourceLocation;
            Variable temp = function.Scope.Put($"$tmp{numTemp++}", location, VariableRole.Local | VariableRole.Temp);
            Debug.Assert(tempType.IsAssignableFrom(expr.Type));
            temp.Type = tempType;
            function.AddLocalVariable(temp);
            AssignStmt stmt = new AssignStmt(location, new VariableAccessExpr(location, temp), expr);
            return (new VariableAccessExpr(location, temp), stmt);
        }

        private (IPExpr, List<IPStmt>) SimplifyLvalue(IPExpr expr)
        {
            // TODO: I am suspicious.
            Antlr4.Runtime.ParserRuleContext location = expr.SourceLocation;
            PLanguageType type = expr.Type;
#pragma warning disable CCN0002 // Non exhaustive patterns in switch block
            switch (expr)
            {
                case MapAccessExpr mapAccessExpr:
                    (IPExpr mapExpr, List<IPStmt> mapExprDeps) = SimplifyLvalue(mapAccessExpr.MapExpr);
                    (IExprTerm mapIndex, List<IPStmt> mapIndexDeps) = SimplifyExpression(mapAccessExpr.IndexExpr);
                    return (new MapAccessExpr(location, mapExpr, mapIndex, type),
                        mapExprDeps.Concat(mapIndexDeps).ToList());

                case NamedTupleAccessExpr namedTupleAccessExpr:
                    (IPExpr ntExpr, List<IPStmt> ntExprDeps) = SimplifyLvalue(namedTupleAccessExpr.SubExpr);
                    return (new NamedTupleAccessExpr(location, ntExpr, namedTupleAccessExpr.Entry), ntExprDeps);

                case SeqAccessExpr seqAccessExpr:
                    (IPExpr seqExpr, List<IPStmt> seqExprDeps) = SimplifyLvalue(seqAccessExpr.SeqExpr);
                    (IExprTerm seqIndex, List<IPStmt> seqIndexDeps) = SimplifyExpression(seqAccessExpr.IndexExpr);
                    return (new SeqAccessExpr(location, seqExpr, seqIndex, type),
                        seqExprDeps.Concat(seqIndexDeps).ToList());

                case TupleAccessExpr tupleAccessExpr:
                    (IPExpr tupExpr, List<IPStmt> tupExprDeps) = SimplifyLvalue(tupleAccessExpr.SubExpr);
                    return (new TupleAccessExpr(location, tupExpr, tupleAccessExpr.FieldNo, type), tupExprDeps);

                case VariableAccessExpr variableAccessExpr:
                    return (variableAccessExpr, new List<IPStmt>());

                default:
                    throw new ArgumentOutOfRangeException(nameof(expr));
            }
#pragma warning restore CCN0002 // Non exhaustive patterns in switch block
        }

        private (IExprTerm, List<IPStmt>) SimplifyRvalue(IPExpr expr)
        {
            // TODO: I am suspicious.
            Antlr4.Runtime.ParserRuleContext location = expr.SourceLocation;
            PLanguageType type = expr.Type;
#pragma warning disable CCN0002 // Non exhaustive patterns in switch block
            switch (expr)
            {
                case NamedTupleAccessExpr _:
                case SeqAccessExpr _:
                case TupleAccessExpr _:
                case MapAccessExpr _:
                    (IExprTerm dataTypeItem, List<IPStmt> dataTypeItemDeps) = SimplifyExpression(expr);
                    (IExprTerm cloneddataTypeItem, IPStmt cloneddataTypeItemDeps) = SaveInTemporary(new CloneExpr(dataTypeItem));
                    return (cloneddataTypeItem, dataTypeItemDeps.Append(cloneddataTypeItemDeps).ToList());
                default:
                    return SimplifyExpression(expr);

            }
#pragma warning restore CCN0002 // Non exhaustive patterns in switch block
        }
        private (IExprTerm, List<IPStmt>) SimplifyExpression(IPExpr expr)
        {
            Antlr4.Runtime.ParserRuleContext location = expr.SourceLocation;
            List<IPStmt> deps = new List<IPStmt>();
#pragma warning disable CCN0002 // Non exhaustive patterns in switch block
            switch (expr)
            {   
                case IExprTerm term:
                    return (term, deps);

                case BinOpExpr binOpExpr:
                    (IExprTerm lhsTemp, List<IPStmt> lhsDeps) = SimplifyExpression(binOpExpr.Lhs);
                    (IExprTerm rhsTemp, List<IPStmt> rhsDeps) = SimplifyExpression(binOpExpr.Rhs);

                    if (binOpExpr.Operation == BinOpType.And)
                    {
                        // And is short-circuiting, so we need to treat it differently from other binary operators
                        deps.AddRange(lhsDeps);
                        (VariableAccessExpr andTemp, IPStmt andInitialStore) = SaveInTemporary(new CloneExpr(lhsTemp));
                        deps.Add(andInitialStore);
                        CompoundStmt reassignFromRhs = new CompoundStmt(location, rhsDeps.Append(new AssignStmt(location, andTemp, new CloneExpr(rhsTemp))));
                        deps.Add(new IfStmt(location, andTemp, reassignFromRhs, null));
                        return (andTemp, deps);
                    }
                    else if (binOpExpr.Operation == BinOpType.Or)
                    {
                        // Or is short-circuiting, so we need to treat it differently from other binary operators
                        deps.AddRange(lhsDeps);
                        (VariableAccessExpr orTemp, IPStmt orInitialStore) = SaveInTemporary(new CloneExpr(lhsTemp));
                        deps.Add(orInitialStore);
                        CompoundStmt reassignFromRhs = new CompoundStmt(location, rhsDeps.Append(new AssignStmt(location, orTemp, new CloneExpr(rhsTemp))));
                        deps.Add(new IfStmt(location, orTemp, new NoStmt(location), reassignFromRhs));
                        return (orTemp, deps);
                    }
                    else
                    {
                        (VariableAccessExpr binOpTemp, IPStmt binOpStore) =
                            SaveInTemporary(new BinOpExpr(location, binOpExpr.Operation, lhsTemp, rhsTemp));
                        deps.AddRange(lhsDeps.Concat(rhsDeps));
                        deps.Add(binOpStore);
                        return (binOpTemp, deps);
                    }

                case CastExpr castExpr:
                    (IExprTerm castSubExpr, List<IPStmt> castDeps) = SimplifyExpression(castExpr.SubExpr);
                    (VariableAccessExpr castTemp, IPStmt castStore) = SaveInTemporary(new CastExpr(location, castSubExpr, castExpr.Type));
                    deps.AddRange(castDeps);
                    deps.Add(castStore);
                    return (castTemp, deps);

                case ChooseExpr chooseExpr:
                    if (chooseExpr.SubExpr != null)
                    {
                        (IExprTerm chooseSubExpr, List<IPStmt> chooseDeps) = SimplifyExpression(chooseExpr.SubExpr);
                        (VariableAccessExpr chooseTemp, IPStmt chooseStore) = SaveInTemporary(new ChooseExpr(location, chooseSubExpr, chooseExpr.Type));
                        deps.AddRange(chooseDeps);
                        deps.Add(chooseStore);
                        return (chooseTemp, deps);
                    }
                    else
                    {
                        (VariableAccessExpr chooseTemp, IPStmt chooseStore) = SaveInTemporary(chooseExpr);
                        deps.Add(chooseStore);
                        return (chooseTemp, deps);
                    }

                case CoerceExpr coerceExpr:
                    (IExprTerm coerceSubExpr, List<IPStmt> coerceDeps) = SimplifyExpression(coerceExpr.SubExpr);
                    (VariableAccessExpr coerceTemp, IPStmt coerceStore) =
                        SaveInTemporary(new CoerceExpr(location, coerceSubExpr, coerceExpr.NewType));
                    deps.AddRange(coerceDeps);
                    deps.Add(coerceStore);
                    return (coerceTemp, deps);

                case ContainsExpr containsKeyExpr:
                    (IExprTerm contKeyExpr, List<IPStmt> contKeyDeps) = SimplifyExpression(containsKeyExpr.Item);
                    (IExprTerm contMapExpr, List<IPStmt> contMapDeps) = SimplifyExpression(containsKeyExpr.Collection);
                    (VariableAccessExpr contTemp, IPStmt contStore) =
                        SaveInTemporary(new ContainsExpr(location, contKeyExpr, contMapExpr));
                    deps.AddRange(contKeyDeps.Concat(contMapDeps));
                    deps.Add(contStore);
                    return (contTemp, deps);

                case CtorExpr ctorExpr:
                    (IReadOnlyList<IVariableRef> ctorArgs, List<IPStmt> ctorArgDeps) = SimplifyArgPack(ctorExpr.Arguments);
                    deps.AddRange(ctorArgDeps);
                    (VariableAccessExpr ctorTemp, IPStmt ctorStore) = SaveInTemporary(new CtorExpr(location, ctorExpr.Interface, ctorArgs));
                    deps.Add(ctorStore);
                    return (ctorTemp, deps);

                case DefaultExpr defaultExpr:
                    (VariableAccessExpr defTemp, IPStmt defStore) = SaveInTemporary(defaultExpr);
                    deps.Add(defStore);
                    return (defTemp, deps);

                case FairNondetExpr fairNondetExpr:
                    (VariableAccessExpr fndTemp, IPStmt fndStore) = SaveInTemporary(fairNondetExpr);
                    deps.Add(fndStore);
                    return (fndTemp, deps);

                case FunCallExpr funCallExpr:
                    (ILinearRef[] funArgs, List<IPStmt> funArgsDeps) = SimplifyFunArgs(funCallExpr.Arguments);
                    deps.AddRange(funArgsDeps);
                    (VariableAccessExpr funTemp, IPStmt funStore) = SaveInTemporary(new FunCallExpr(location, funCallExpr.Function, funArgs));
                    deps.Add(funStore);
                    return (funTemp, deps);

                case KeysExpr keysExpr:
                    (IExprTerm keysColl, List<IPStmt> keysDeps) = SimplifyExpression(keysExpr.Expr);
                    (VariableAccessExpr keysTemp, IPStmt keysStore) = SaveInTemporary(new KeysExpr(location, keysColl, keysExpr.Type));
                    deps.AddRange(keysDeps);
                    deps.Add(keysStore);
                    return (keysTemp, deps);

                case MapAccessExpr mapAccessExpr:
                    (IExprTerm mapExpr, List<IPStmt> mapDeps) = SimplifyExpression(mapAccessExpr.MapExpr);
                    (IExprTerm mapIdxExpr, List<IPStmt> mapIdxDeps) = SimplifyExpression(mapAccessExpr.IndexExpr);
                    (VariableAccessExpr mapItemTemp, IPStmt mapItemStore) =
                        SaveInTemporary(new MapAccessExpr(location, mapExpr, mapIdxExpr, mapAccessExpr.Type));
                    deps.AddRange(mapDeps.Concat(mapIdxDeps));
                    deps.Add(mapItemStore);
                    return (mapItemTemp, deps);

                case NamedTupleAccessExpr namedTupleAccessExpr:
                    (IExprTerm ntSubExpr, List<IPStmt> ntSubDeps) = SimplifyExpression(namedTupleAccessExpr.SubExpr);
                    (VariableAccessExpr ntTemp, IPStmt ntStore) =
                        SaveInTemporary(new NamedTupleAccessExpr(location, ntSubExpr, namedTupleAccessExpr.Entry));
                    deps.AddRange(ntSubDeps);
                    deps.Add(ntStore);
                    return (ntTemp, deps);

                case NamedTupleExpr namedTupleExpr:
                    (IReadOnlyList<IVariableRef> args, List<IPStmt> argDeps) = SimplifyArgPack(namedTupleExpr.TupleFields);
                    deps.AddRange(argDeps);

                    (VariableAccessExpr ntVal, IPStmt ntValStore) =
                        SaveInTemporary(new NamedTupleExpr(location, args, namedTupleExpr.Type));
                    deps.Add(ntValStore);
                    return (ntVal, deps);

                case NondetExpr nondetExpr:
                    (VariableAccessExpr ndTemp, IPStmt ndStore) = SaveInTemporary(nondetExpr);
                    deps.Add(ndStore);
                    return (ndTemp, deps);

                case SeqAccessExpr seqAccessExpr:
                    (IExprTerm seqExpr, List<IPStmt> seqDeps) = SimplifyExpression(seqAccessExpr.SeqExpr);
                    (IExprTerm seqIdx, List<IPStmt> seqIdxDeps) = SimplifyExpression(seqAccessExpr.IndexExpr);
                    (VariableAccessExpr seqElem, IPStmt seqElemStore) =
                        SaveInTemporary(new SeqAccessExpr(location, seqExpr, seqIdx, seqAccessExpr.Type));
                    deps.AddRange(seqDeps.Concat(seqIdxDeps));
                    deps.Add(seqElemStore);
                    return (seqElem, deps);

                case SizeofExpr sizeofExpr:
                    (IExprTerm sizeExpr, List<IPStmt> sizeDeps) = SimplifyExpression(sizeofExpr.Expr);
                    (VariableAccessExpr sizeTemp, IPStmt sizeStore) = SaveInTemporary(new SizeofExpr(location, sizeExpr));
                    deps.AddRange(sizeDeps);
                    deps.Add(sizeStore);
                    return (sizeTemp, deps);

                case TupleAccessExpr tupleAccessExpr:
                    (IExprTerm tupItemExpr, List<IPStmt> tupAccessDeps) = SimplifyExpression(tupleAccessExpr.SubExpr);
                    (VariableAccessExpr tupItemTemp, IPStmt tupItemStore) =
                        SaveInTemporary(new TupleAccessExpr(location,
                            tupItemExpr,
                            tupleAccessExpr.FieldNo,
                            tupleAccessExpr.Type));
                    deps.AddRange(tupAccessDeps);
                    deps.Add(tupItemStore);
                    return (tupItemTemp, deps);

                case UnaryOpExpr unaryOpExpr:
                    (IExprTerm unExpr, List<IPStmt> unDeps) = SimplifyExpression(unaryOpExpr.SubExpr);
                    (VariableAccessExpr unTemp, IPStmt unStore) = SaveInTemporary(new UnaryOpExpr(location, unaryOpExpr.Operation, unExpr));
                    deps.AddRange(unDeps);
                    deps.Add(unStore);
                    return (unTemp, deps);

                case UnnamedTupleExpr unnamedTupleExpr:
                    (IReadOnlyList<IVariableRef> tupFields, List<IPStmt> tupFieldDeps) = SimplifyArgPack(unnamedTupleExpr.TupleFields);
                    deps.AddRange(tupFieldDeps);
                    (VariableAccessExpr tupVal, IPStmt tupStore) = SaveInTemporary(new UnnamedTupleExpr(location, tupFields));
                    deps.Add(tupStore);
                    return (tupVal, deps);

                case ValuesExpr valuesExpr:
                    (IExprTerm valuesColl, List<IPStmt> valuesDeps) = SimplifyExpression(valuesExpr.Expr);
                    (VariableAccessExpr valuesTemp, IPStmt valuesStore) =
                        SaveInTemporary(new ValuesExpr(location, valuesColl, valuesExpr.Type));
                    deps.AddRange(valuesDeps);
                    deps.Add(valuesStore);
                    return (valuesTemp, deps);
                case StringExpr stringExpr:
                    (IPExpr[] stringArgs, List< IPStmt > stringArgsDeps) = SimplifyFunArgs(stringExpr.Args);
                    (VariableAccessExpr stringTemp, IPStmt stringStore) = SaveInTemporary(new StringExpr(location, stringExpr.BaseString, stringArgs.ToList()));
                    deps.AddRange(stringArgsDeps);
                    deps.Add(stringStore);
                    return (stringTemp, deps);
                default:
                    throw new ArgumentOutOfRangeException(nameof(expr));
            }
#pragma warning restore CCN0002 // Non exhaustive patterns in switch block
        }

        private List<IPStmt> SimplifyStatement(IPStmt statement)
        {
            Antlr4.Runtime.ParserRuleContext location = statement?.SourceLocation;
            switch (statement)
            {

                case CompoundStmt compoundStmt:
                    List<IPStmt> newBlock = new List<IPStmt>();
                    foreach (IPStmt step in compoundStmt.Statements)
                    {
                        newBlock.AddRange(SimplifyStatement(step));
                    }
                    // TODO: why not return the list? because of source location info?
                    return new List<IPStmt> { new CompoundStmt(location, newBlock) };
                    
                case SendStmt sendStmt:
                    // (IExprTerm sendMachine, List<IPStmt> sendMachineDeps) = SimplifyExpression(sendStmt.MachineExpr);
                    // (VariableAccessExpr sendMachineAccessExpr, IPStmt sendMachineAssn) = SaveInTemporary(new CloneExpr(sendMachine));

                    // (IExprTerm sendEvent, List<IPStmt> sendEventDeps) = SimplifyExpression(sendStmt.Evt);
                    // (VariableAccessExpr sendEventAccessExpr, IPStmt sendEventAssn) = SaveInTemporary(new CloneExpr(sendEvent));

                    (IReadOnlyList<IVariableRef> sendArgs, List<IPStmt> sendArgDeps) = SimplifyArgPack(sendStmt.Arguments);

                    return sendArgDeps
                        .Concat(new[]
                        {
                            new SendStmt(location, sendStmt.MachineExpr, sendStmt.Evt, sendArgs)
                        })
                        .ToList();

                case null:
                    throw new ArgumentNullException(nameof(statement));
                case AnnounceStmt announceStmt:
                case AssertStmt assertStmt:
                case AssignStmt assignStmt:
                case CtorStmt ctorStmt:
                case FunCallStmt funCallStmt:
                case GotoStmt gotoStmt:
                case IfStmt ifStmt:
                case AddStmt addStmt:
                case InsertStmt insertStmt:
                case MoveAssignStmt moveAssignStmt:
                case NoStmt _:
                case PopStmt popStmt:
                case PrintStmt printStmt:
                case RaiseStmt raiseStmt:
                case ReceiveStmt receiveStmt:
                case RemoveStmt removeStmt:
                case ReturnStmt returnStmt:
                case BreakStmt breakStmt:
                case ContinueStmt continueStmt:
                case SwapAssignStmt swapAssignStmt:
                case WhileStmt whileStmt:
                    return new List<IPStmt> { statement };

                default:
                    throw new ArgumentOutOfRangeException(nameof(statement));
            }
        }

        private (IReadOnlyList<IVariableRef> args, List<IPStmt> deps) SimplifyArgPack(IEnumerable<IPExpr> argsPack)
        {
            List<IPExpr> args = argsPack.ToList();
            Debug.Assert(!args.Any(arg => arg is LinearAccessRefExpr lin && lin.LinearType.Equals(LinearType.Swap)));
            (ILinearRef[] refArgs, List<IPStmt> deps) = SimplifyFunArgs(args);
            return (refArgs, deps);
        }

        private (ILinearRef[], List<IPStmt>) SimplifyFunArgs(IReadOnlyList<IPExpr> funCallArgs)
        {
            ILinearRef[] funArgs = new ILinearRef[funCallArgs.Count];
            List<IPStmt> deps = new List<IPStmt>();
            for (int i = 0; i < funCallArgs.Count; i++)
            {
                (IExprTerm argExpr, List<IPStmt> argDeps) = SimplifyExpression(funCallArgs[i]);
                deps.AddRange(argDeps);
                // My thoughts: if SimplifyExpression returns a temporary, there's no way that came from user code, thus
                // it must be either an intermediate expression, or an explicit clone of another value. In either case, the
                // memory there is invisible to the rest of the program and so it can be moved safely. The first case below
                // is therefore an optimization that assumes this holds. If things go wrong, comment it out and take the extra
                // allocations as a hit.
                switch (argExpr)
                {
                    // Move temporaries...
                    case VariableAccessExpr variableAccessExpr
                        when variableAccessExpr.Variable.Role.HasFlag(VariableRole.Temp):
                        funArgs[i] = new LinearAccessRefExpr(variableAccessExpr.SourceLocation,
                            variableAccessExpr.Variable, LinearType.Move);
                        break;
                    // ...and leave linear accesses alone...
                    case LinearAccessRefExpr linearAccessRefExpr:
                        funArgs[i] = linearAccessRefExpr;
                        break;
                    // ...but clone literals and visible variables/fields.
                    default:
                        (VariableAccessExpr temp, IPStmt tempDep) = SaveInTemporary(new CloneExpr(argExpr));
                        deps.Add(tempDep);
                        funArgs[i] = new LinearAccessRefExpr(temp.SourceLocation, temp.Variable, LinearType.Move);
                        break;
                }
            }

            return (funArgs, deps);
        }
    }
}
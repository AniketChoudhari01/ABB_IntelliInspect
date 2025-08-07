import pandas as pd
import lightgbm as lgb
import os
import json
from sklearn.metrics import (
    accuracy_score, precision_score, recall_score,
    f1_score, confusion_matrix
)
import joblib

def train_model(filepath: str, range_selection: dict, storage_path: str) -> dict:
    print(f"Starting training with file: {filepath}")
    
    try:
        # Load data in chunks to handle large files
        CHUNK_SIZE = 50_000  # Reduced chunk size for better memory management
        chunks = pd.read_csv(filepath, chunksize=CHUNK_SIZE)

        df_list = []
        for chunk in chunks:
            if 'Response' not in chunk.columns:
                raise ValueError("Missing 'Response' column in CSV.")
            df_list.append(chunk)

        if not df_list:
            raise ValueError("No rows loaded from the CSV.")

        df = pd.concat(df_list, axis=0, ignore_index=True)
        print(f"Loaded {len(df)} total rows")
        
        # Convert timestamp column
        df['SyntheticTimestamp'] = pd.to_datetime(df['SyntheticTimestamp'])

        # Slice data based on provided date ranges
        train_mask = (
            (df['SyntheticTimestamp'] >= range_selection['TrainStart']) & 
            (df['SyntheticTimestamp'] <= range_selection['TrainEnd'])
        )
        test_mask = (
            (df['SyntheticTimestamp'] >= range_selection['TestStart']) & 
            (df['SyntheticTimestamp'] <= range_selection['TestEnd'])
        )

        train_df = df[train_mask].copy()
        test_df = df[test_mask].copy()
        
        print(f"Train data: {len(train_df)} rows")
        print(f"Test data: {len(test_df)} rows")
        
        if len(train_df) == 0:
            raise ValueError("No training data found for the specified date range")
        if len(test_df) == 0:
            raise ValueError("No test data found for the specified date range")

        # Fill missing values
        for subset_name, subset in [("train", train_df), ("test", test_df)]:
            for col in subset.columns:
                if subset[col].isna().any():
                    if subset[col].dtype in ['float64', 'int64']:
                        median_val = subset[col].median()
                        subset[col] = subset[col].fillna(median_val)
                    else:
                        subset[col] = subset[col].fillna("-1")

        # Prepare features and target
        feature_cols = [col for col in train_df.columns if col not in ['Response', 'Id', 'SyntheticTimestamp']]
        
        X_train = train_df[feature_cols].copy()
        y_train = train_df['Response'].copy()
        X_test = test_df[feature_cols].copy()
        y_test = test_df['Response'].copy()

        # Calculate class weights for imbalanced data
        pos = int(sum(y_train == 1))
        neg = int(sum(y_train == 0))
        scale_pos_weight = neg / pos if pos > 0 else 1.0

        print(f"Positive samples: {pos}, Negative samples: {neg}")
        print(f"Scale pos weight: {scale_pos_weight}")

        # Initialize results storage
        training_history = {
            'train_accuracy': [],
            'valid_accuracy': [],
            'train_logloss': [],
            'valid_logloss': []
        }

        # Custom callback to capture training metrics
        def log_evaluation_callback(period=1):
            def callback(env):
                if env.iteration % period == 0:
                    train_results = env.evaluation_result_list
                    for item in train_results:
                        data_name, eval_name, result, _ = item
                        if data_name == 'training':
                            if eval_name == 'binary_logloss':
                                training_history['train_logloss'].append(result)
                            elif eval_name == 'accuracy':
                                training_history['train_accuracy'].append(result)
                        elif data_name == 'valid_1':
                            if eval_name == 'binary_logloss':
                                training_history['valid_logloss'].append(result)
                            elif eval_name == 'accuracy':
                                training_history['valid_accuracy'].append(result)
                return False
            return callback

        # Custom accuracy metric
        def lgb_accuracy(y_true, y_pred):
            y_pred_binary = (y_pred > 0.5).astype(int)
            acc = accuracy_score(y_true, y_pred_binary)
            return 'accuracy', acc, True

        # Create and configure model
        model = lgb.LGBMClassifier(
            objective='binary',
            scale_pos_weight=scale_pos_weight,
            n_estimators=50,  # Reduced for faster training
            max_depth=6,
            num_leaves=31,
            learning_rate=0.1,
            subsample=0.8,
            colsample_bytree=0.8,
            random_state=42,
            verbosity=-1,
            force_col_wise=True  # Better for wide datasets
        )

        # Train the model
        print("Starting model training...")
        model.fit(
            X_train, y_train,
            eval_set=[(X_train, y_train), (X_test, y_test)],
            eval_names=['training', 'valid_1'],
            eval_metric=[lgb_accuracy, 'binary_logloss'],
            callbacks=[
                lgb.early_stopping(stopping_rounds=10, verbose=False),
                log_evaluation_callback(period=1)
            ]
        )

        print("Training completed!")
        
        # Save the trained model
        model_path = os.path.join(storage_path, "model.pkl")
        joblib.dump(model, model_path)
        print(f"✅ Model saved to: {model_path}")

        # Make predictions
        y_pred_test = model.predict(X_test)
        y_pred_train = model.predict(X_train)

        # Calculate metrics
        test_accuracy = accuracy_score(y_test, y_pred_test)
        test_precision = precision_score(y_test, y_pred_test, zero_division=0)
        test_recall = recall_score(y_test, y_pred_test, zero_division=0)
        test_f1 = f1_score(y_test, y_pred_test, zero_division=0)

        # Confusion matrix
        conf_matrix = confusion_matrix(y_test, y_pred_test)
        tn, fp, fn, tp = conf_matrix.ravel()

        # Prepare results
        results = {
            "training_info": {
                "total_rows_used": len(df),
                "train_rows": len(train_df),
                "test_rows": len(test_df),
                "positive_samples": pos,
                "negative_samples": neg,
                "scale_pos_weight": round(scale_pos_weight, 2),
                "features_used": len(feature_cols)
            },
            "model_performance": {
                "accuracy": round(test_accuracy * 100, 2),
                "precision": round(test_precision * 100, 2),
                "recall": round(test_recall * 100, 2),
                "f1_score": round(test_f1 * 100, 2)
            },
            "confusion_matrix": {
                "true_positive": int(tp),
                "true_negative": int(tn),
                "false_positive": int(fp),
                "false_negative": int(fn)
            },
            "training_metrics": {
                "train_accuracy_history": training_history['train_accuracy'],
                "valid_accuracy_history": training_history['valid_accuracy'],
                "train_logloss_history": training_history['train_logloss'],
                "valid_logloss_history": training_history['valid_logloss'],
                "epochs_trained": len(training_history['train_accuracy'])
            },
            "date_ranges": range_selection,
            "status": "success",
            "message": "Model trained successfully"
        }

        # Save results to JSON file
        output_dir = os.path.join(storage_path)
        os.makedirs(output_dir, exist_ok=True)
        metrics_path = os.path.join(output_dir, "metrics.json")
        
        with open(metrics_path, "w") as f:
            json.dump(results, f, indent=4)

        print(f"✅ Results saved to: {metrics_path}")
        print(f"✅ Model Accuracy: {results['model_performance']['accuracy']}%")
        print(f"✅ Model F1-Score: {results['model_performance']['f1_score']}%")

        return results

    except Exception as e:
        error_result = {
            "status": "error",
            "message": f"Training failed: {str(e)}",
            "error_details": str(e)
        }
        
        # Save error to file too
        try:
            output_dir = os.path.join(storage_path)
            os.makedirs(output_dir, exist_ok=True)
            metrics_path = os.path.join(output_dir, "metrics.json")
            with open(metrics_path, "w") as f:
                json.dump(error_result, f, indent=4)
        except:
            pass
            
        print(f"❌ Training error: {e}")
        raise e